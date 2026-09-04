using System.Diagnostics;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.EventBus;

internal sealed class EventChannelRuntime : IDisposable
{
    private static readonly AsyncLocal<EventChannelRuntime?> CurrentRuntime = new();
    private readonly LinkedList<PublicationRequest> _queue = [];
    private readonly HashSet<string> _activePartitions = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _items = new(0);
    private readonly SemaphoreSlim _spaces;
    private readonly SemaphoreSlim _executionSlots;
    private readonly SemaphoreSlim _partitionProgress = new(0);
    private readonly object _syncRoot = new();
    private readonly EventChannelOptions _options;
    private readonly CancellationToken _shutdownToken;
    private readonly EventDiagnosticWriter _diagnostics;
    private readonly Task _workerTask;
    private TaskCompletionSource? _drainCompletion;
    private int _inFlightCount;
    private bool _completed;
    private bool _waitingForPartitionProgress;
    private long _acceptedCount;
    private long _rejectedCount;
    private long _droppedCount;
    private long _completedCount;
    private long _failedCount;
    private long _totalQueueWaitTicks;
    private long _maximumQueueWaitTicks;

    public EventChannelRuntime(
        EventContractDescriptor descriptor,
        string channelName,
        EventChannelOptions options,
        CancellationToken shutdownToken,
        EventDiagnosticWriter diagnostics)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        ChannelName = new EventChannel<object>(channelName).Name;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _shutdownToken = shutdownToken;
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _spaces = new SemaphoreSlim(options.Capacity, options.Capacity);
        _executionSlots = new SemaphoreSlim(options.MaximumConcurrency, options.MaximumConcurrency);
        if (ExecutionContext.IsFlowSuppressed())
        {
            _workerTask = Task.Run(RunWorkerAsync);
        }
        else
        {
            using (ExecutionContext.SuppressFlow())
            {
                _workerTask = Task.Run(RunWorkerAsync);
            }
        }
    }

    public EventContractDescriptor Descriptor { get; }

    public string ChannelName { get; }

    public EventChannelBackpressurePolicy BackpressurePolicy => _options.BackpressurePolicy;

    public Task Completion => _workerTask;

    public bool IsExecutingOnCurrentContext => ReferenceEquals(CurrentRuntime.Value, this);

    public async ValueTask<EnqueueResult> EnqueueAsync(
        PublicationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePartition(request);

        var result = _options.BackpressurePolicy switch
        {
            EventChannelBackpressurePolicy.Wait =>
                IsExecutingOnCurrentContext
                    ? TryEnqueueOrReject(request)
                    : await EnqueueWithWaitAsync(request, cancellationToken).ConfigureAwait(false),
            EventChannelBackpressurePolicy.Reject or EventChannelBackpressurePolicy.DropNewest =>
                TryEnqueueOrReject(request),
            EventChannelBackpressurePolicy.DropOldest => EnqueueDroppingOldest(request),
            EventChannelBackpressurePolicy.CoalesceLatest => EnqueueCoalescingLatest(request),
            _ => throw new InvalidOperationException("Event channel backpressure policy is not supported.")
        };

        if (result == EnqueueResult.Accepted)
        {
            Interlocked.Increment(ref _acceptedCount);
        }
        else
        {
            Interlocked.Increment(ref _rejectedCount);
            WriteBackpressureDiagnostic(
                request,
                result == EnqueueResult.TimedOut
                    ? "queue wait timed out"
                    : result == EnqueueResult.Closed
                        ? "channel was closed"
                        : "publication was rejected");
        }

        return result;
    }

    public PublicationRequest[] Complete()
    {
        PublicationRequest[] pending;

        lock (_syncRoot)
        {
            if (_completed)
            {
                return [];
            }

            _completed = true;
            _waitingForPartitionProgress = false;
            pending = _queue.ToArray();
            _queue.Clear();
            if (_inFlightCount > 0)
            {
                _drainCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (pending.Length > 0)
        {
            _spaces.Release(pending.Length);
        }

        _items.Release();
        _partitionProgress.Release();
        return pending;
    }

    public EventChannelMetricsSnapshot GetSnapshot()
    {
        int pendingCount;
        int inFlightCount;

        lock (_syncRoot)
        {
            pendingCount = _queue.Count;
            inFlightCount = _inFlightCount;
        }

        return new EventChannelMetricsSnapshot(
            Descriptor.ContractId,
            ChannelName,
            _options.ExecutionMode,
            _options.Capacity,
            pendingCount,
            inFlightCount,
            Interlocked.Read(ref _acceptedCount),
            Interlocked.Read(ref _rejectedCount),
            Interlocked.Read(ref _droppedCount),
            Interlocked.Read(ref _completedCount),
            Interlocked.Read(ref _failedCount))
        {
            TotalQueueWaitDuration = TimeSpan.FromTicks(Interlocked.Read(ref _totalQueueWaitTicks)),
            MaximumQueueWaitDuration = TimeSpan.FromTicks(Interlocked.Read(ref _maximumQueueWaitTicks))
        };
    }

    public void Dispose()
    {
        if (!_workerTask.IsCompleted)
        {
            throw new InvalidOperationException("Event channel runtime cannot release resources before its worker completes.");
        }

        _items.Dispose();
        _spaces.Dispose();
        _executionSlots.Dispose();
        _partitionProgress.Dispose();
    }

    private async ValueTask<EnqueueResult> EnqueueWithWaitAsync(
        PublicationRequest request,
        CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _shutdownToken);

        bool entered;
        try
        {
            entered = _options.QueueWaitTimeout is { } timeout
                ? await _spaces.WaitAsync(timeout, linkedCancellation.Token).ConfigureAwait(false)
                : await WaitForSpaceAsync(linkedCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            _shutdownToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return EnqueueResult.Closed;
        }

        if (!entered)
        {
            return EnqueueResult.TimedOut;
        }

        return CommitReservedSpace(request);
    }

    private void ValidatePartition(PublicationRequest request)
    {
        if (_options.ExecutionMode == EventChannelExecutionMode.Partitioned)
        {
            if (request.PartitionKey is null)
            {
                throw new InvalidOperationException(
                    $"Partitioned event channel '{ChannelName}' requires EventPublishOptions.PartitionKey.");
            }

            return;
        }

        if (request.PartitionKey is not null)
        {
            throw new InvalidOperationException(
                $"Event channel '{ChannelName}' does not use Partitioned execution and cannot accept a partition key.");
        }
    }

    private async ValueTask<bool> WaitForSpaceAsync(CancellationToken cancellationToken)
    {
        await _spaces.WaitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private EnqueueResult TryEnqueueOrReject(PublicationRequest request)
    {
        if (!_spaces.Wait(0))
        {
            lock (_syncRoot)
            {
                return _completed ? EnqueueResult.Closed : EnqueueResult.Rejected;
            }
        }

        return CommitReservedSpace(request);
    }

    private EnqueueResult EnqueueDroppingOldest(PublicationRequest request)
    {
        if (_spaces.Wait(0))
        {
            return CommitReservedSpace(request);
        }

        PublicationRequest? displaced;
        lock (_syncRoot)
        {
            if (_completed)
            {
                return EnqueueResult.Closed;
            }

            displaced = _queue.First?.Value;
            if (displaced is null)
            {
                return EnqueueResult.Rejected;
            }

            _queue.RemoveFirst();
            _queue.AddLast(request);
        }

        Interlocked.Increment(ref _droppedCount);
        displaced.Reject(
            "The publication was dropped because the channel replaced its oldest pending event.",
            observePostedFailure: false);
        WriteDisplacedDiagnostic(displaced, "dropped as the oldest pending event");
        return EnqueueResult.Accepted;
    }

    private EnqueueResult EnqueueCoalescingLatest(PublicationRequest request)
    {
        PublicationRequest? displaced = null;

        lock (_syncRoot)
        {
            if (_completed)
            {
                return EnqueueResult.Closed;
            }

            var candidate = _queue.Last;
            while (candidate is not null)
            {
                if (string.Equals(candidate.Value.PartitionKey, request.PartitionKey, StringComparison.Ordinal))
                {
                    displaced = candidate.Value;
                    _queue.AddAfter(candidate, request);
                    _queue.Remove(candidate);
                    break;
                }

                candidate = candidate.Previous;
            }
        }

        if (displaced is not null)
        {
            Interlocked.Increment(ref _droppedCount);
            displaced.Reject(
                "The publication was coalesced by a newer pending event.",
                observePostedFailure: false);
            WriteDisplacedDiagnostic(displaced, "coalesced by a newer pending event");
            return EnqueueResult.Accepted;
        }

        if (!_spaces.Wait(0))
        {
            return EnqueueResult.Rejected;
        }

        return CommitReservedSpace(request);
    }

    private EnqueueResult CommitReservedSpace(PublicationRequest request)
    {
        var notifyPartitionProgress = false;
        lock (_syncRoot)
        {
            if (_completed)
            {
                _spaces.Release();
                return EnqueueResult.Closed;
            }

            _queue.AddLast(request);
            if (_options.ExecutionMode == EventChannelExecutionMode.Partitioned &&
                _waitingForPartitionProgress)
            {
                _waitingForPartitionProgress = false;
                notifyPartitionProgress = true;
            }
        }

        _items.Release();
        if (notifyPartitionProgress)
        {
            _partitionProgress.Release();
        }

        return EnqueueResult.Accepted;
    }

    private async Task RunWorkerAsync()
    {
        while (true)
        {
            await _executionSlots.WaitAsync().ConfigureAwait(false);
            await _items.WaitAsync().ConfigureAwait(false);

            PublicationRequest? request;
            var waitForPartition = false;
            lock (_syncRoot)
            {
                var node = FindRunnableNode();
                if (node is null)
                {
                    request = null;
                    waitForPartition = !_completed && _queue.Count > 0;
                    if (waitForPartition)
                    {
                        _waitingForPartitionProgress = true;
                    }
                }
                else
                {
                    request = node.Value;
                    _queue.Remove(node);
                    _spaces.Release();
                    _inFlightCount++;
                    if (_options.ExecutionMode == EventChannelExecutionMode.Partitioned)
                    {
                        _activePartitions.Add(request.PartitionKey!);
                    }
                }
            }

            if (request is null)
            {
                _executionSlots.Release();
                if (_completed)
                {
                    break;
                }

                _items.Release();
                if (waitForPartition)
                {
                    await _partitionProgress.WaitAsync().ConfigureAwait(false);
                }

                continue;
            }

            _ = ExecuteRequestAsync(request);
        }

        Task? drainTask;
        lock (_syncRoot)
        {
            drainTask = _inFlightCount == 0 ? null : _drainCompletion?.Task;
        }

        if (drainTask is not null)
        {
            await drainTask.ConfigureAwait(false);
        }
    }

    private LinkedListNode<PublicationRequest>? FindRunnableNode()
    {
        if (_options.ExecutionMode != EventChannelExecutionMode.Partitioned)
        {
            return _queue.First;
        }

        var node = _queue.First;
        while (node is not null)
        {
            if (!_activePartitions.Contains(node.Value.PartitionKey!))
            {
                return node;
            }

            node = node.Next;
        }

        return null;
    }

    private async Task ExecuteRequestAsync(PublicationRequest request)
    {
        var succeeded = false;
        var queueWaitDuration = request.GetQueueWaitDuration();
        Interlocked.Add(ref _totalQueueWaitTicks, queueWaitDuration.Ticks);
        UpdateMaximum(ref _maximumQueueWaitTicks, queueWaitDuration.Ticks);
        var previousRuntime = CurrentRuntime.Value;
        CurrentRuntime.Value = this;
        try
        {
            succeeded = await request.ExecuteAsync(_shutdownToken, queueWaitDuration).ConfigureAwait(false);
        }
        catch
        {
            // PublicationRequest observes publisher and posted failures. This guard keeps the worker alive
            // if a diagnostic observer itself fails; AUC-EVENTBUS-005 owns sink-failure reporting.
        }
        finally
        {
            CurrentRuntime.Value = previousRuntime;
            CompleteExecution(request, succeeded);
        }
    }

    private void CompleteExecution(PublicationRequest request, bool succeeded)
    {
        TaskCompletionSource? drainCompletion = null;
        var notifyPartitionProgress = false;

        if (succeeded)
        {
            Interlocked.Increment(ref _completedCount);
        }
        else
        {
            Interlocked.Increment(ref _failedCount);
        }

        lock (_syncRoot)
        {
            if (_options.ExecutionMode == EventChannelExecutionMode.Partitioned)
            {
                _activePartitions.Remove(request.PartitionKey!);
                if (_waitingForPartitionProgress)
                {
                    _waitingForPartitionProgress = false;
                    notifyPartitionProgress = true;
                }
            }

            _inFlightCount--;
            if (_completed && _inFlightCount == 0)
            {
                drainCompletion = _drainCompletion;
            }
        }

        _executionSlots.Release();
        if (notifyPartitionProgress)
        {
            _partitionProgress.Release();
        }

        drainCompletion?.TrySetResult();
    }

    private void WriteDisplacedDiagnostic(PublicationRequest request, string action)
    {
        WriteBackpressureDiagnostic(request, action);
        _diagnostics.Write(
            new HostDiagnosticRecord(
                EventDiagnosticIds.EventDropped,
                $"Event '{request.ContractId.Value}' with id '{request.EventId:D}' was {action}.",
                HostDiagnosticSeverity.Warning)
            {
                Context = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["contractId"] = request.ContractId.Value,
                    ["eventId"] = request.EventId.ToString("D"),
                    ["channel"] = ChannelName,
                    ["partitionHash"] = EventCorrelationIds.ToDiagnosticHash(request.PartitionKey),
                    ["backpressurePolicy"] = _options.BackpressurePolicy.ToString(),
                    ["correlationId"] = request.CorrelationId,
                    ["causationId"] = request.CausationId,
                    ["publishDepth"] = request.PublishDepth.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }
            });
    }

    private void WriteBackpressureDiagnostic(PublicationRequest request, string action)
    {
        _diagnostics.Write(
            new HostDiagnosticRecord(
                EventDiagnosticIds.EventChannelBackpressure,
                $"Event channel '{ChannelName}' applied backpressure to event '{request.EventId:D}': {action}.",
                HostDiagnosticSeverity.Warning)
            {
                Context = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["contractId"] = request.ContractId.Value,
                    ["eventId"] = request.EventId.ToString("D"),
                    ["channel"] = ChannelName,
                    ["partitionHash"] = EventCorrelationIds.ToDiagnosticHash(request.PartitionKey),
                    ["backpressurePolicy"] = _options.BackpressurePolicy.ToString(),
                    ["backpressureAction"] = action,
                    ["correlationId"] = request.CorrelationId,
                    ["causationId"] = request.CausationId,
                    ["publishDepth"] = request.PublishDepth.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }
            });
    }

    private static void UpdateMaximum(ref long target, long candidate)
    {
        var current = Interlocked.Read(ref target);
        while (candidate > current)
        {
            var observed = Interlocked.CompareExchange(ref target, candidate, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    internal sealed class PublicationRequest
    {
        private readonly Func<CancellationToken, TimeSpan, ValueTask<EventPublishResult>> _publish;
        private readonly TaskCompletionSource<EventPublishResult>? _completion;
        private readonly Action<Exception> _observePostedFailure;
        private readonly long _createdTimestamp = Stopwatch.GetTimestamp();

        public PublicationRequest(
            Guid eventId,
            EventContractId contractId,
            string? partitionKey,
            string correlationId,
            string? causationId,
            int publishDepth,
            Func<CancellationToken, TimeSpan, ValueTask<EventPublishResult>> publish,
            bool waitForCompletion,
            Action<Exception> observePostedFailure)
        {
            EventId = eventId;
            ContractId = contractId;
            PartitionKey = partitionKey;
            CorrelationId = correlationId;
            CausationId = causationId;
            PublishDepth = publishDepth;
            _publish = publish;
            _observePostedFailure = observePostedFailure;
            _completion = waitForCompletion
                ? new TaskCompletionSource<EventPublishResult>(TaskCreationOptions.RunContinuationsAsynchronously)
                : null;
        }

        public Guid EventId { get; }

        public EventContractId ContractId { get; }

        public string? PartitionKey { get; }

        public string CorrelationId { get; }

        public string? CausationId { get; }

        public int PublishDepth { get; }

        public Task<EventPublishResult>? Completion => _completion?.Task;

        public TimeSpan GetQueueWaitDuration()
        {
            return Stopwatch.GetElapsedTime(_createdTimestamp);
        }

        public async ValueTask<bool> ExecuteAsync(
            CancellationToken shutdownToken,
            TimeSpan queueWaitDuration)
        {
            try
            {
                var result = await _publish(shutdownToken, queueWaitDuration).ConfigureAwait(false);
                _completion?.TrySetResult(result);
                return result.Succeeded;
            }
            catch (Exception exception)
            {
                if (_completion is not null)
                {
                    _completion.TrySetException(exception);
                }
                else
                {
                    _observePostedFailure(exception);
                }

                return false;
            }
        }

        public void Reject(string reason, bool observePostedFailure = true)
        {
            var exception = new EventPublicationRejectedException(EventId, ContractId, reason);
            if (_completion is not null)
            {
                _completion.TrySetException(exception);
            }
            else if (observePostedFailure)
            {
                _observePostedFailure(exception);
            }
        }
    }

    internal enum EnqueueResult
    {
        Accepted,
        Rejected,
        TimedOut,
        Closed
    }
}
