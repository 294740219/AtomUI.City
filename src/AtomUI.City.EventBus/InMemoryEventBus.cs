using AtomUI.City.Diagnostics;
using AtomUI.City.Lifecycle;

namespace AtomUI.City.EventBus;

public sealed class InMemoryEventBus : IEventBus, IDisposable
{
    private const string ContractIdContextKey = "contractId";
    private const string EventIdContextKey = "eventId";
    private const string EventTypeContextKey = "eventType";
    private const string SubscriptionIdContextKey = "subscriptionId";
    private const string DispatchPolicyContextKey = "dispatchPolicy";
    private const string ErrorPolicyContextKey = "errorPolicy";
    private const string DeliveryExceptionContractIdDataKey = "AtomUI.City.EventBus.ContractId";
    private const string DeliveryExceptionEventIdDataKey = "AtomUI.City.EventBus.EventId";
    private const string DeliveryExceptionSubscriptionIdDataKey = "AtomUI.City.EventBus.SubscriptionId";

    private readonly IEventContractRegistry _contractRegistry;
    private readonly IHostDiagnostics? _diagnostics;
    private readonly Dictionary<Type, List<EventSubscription>> _subscriptions = [];
    private readonly object _syncRoot = new();
    private bool _disposed;

    public InMemoryEventBus(
        IEventContractRegistry? contractRegistry = null,
        IHostDiagnostics? diagnostics = null)
    {
        _contractRegistry = contractRegistry ?? new InMemoryEventContractRegistry();
        _diagnostics = diagnostics;
    }

    public IEventSubscription Subscribe<TEvent>(
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfDisposed();

        return SubscribeCore(
            owner: null,
            handler,
            options ?? EventSubscriptionOptions.Serialized);
    }

    public IEventSubscription Subscribe<TEvent>(
        Action<EventContext<TEvent>> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Subscribe<TEvent>(
            context =>
            {
                handler(context);

                return ValueTask.CompletedTask;
            },
            options);
    }

    public IEventSubscription Subscribe<TEvent>(
        IEventHandler<TEvent> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Subscribe<TEvent>(
            context => handler.HandleAsync(context),
            options);
    }

    public IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);
        ThrowIfDisposed();
        ThrowIfOwnerNotRunning(owner);

        return SubscribeCore(
            owner,
            handler,
            options ?? EventSubscriptionOptions.Serialized);
    }

    public IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        Action<EventContext<TEvent>> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Subscribe<TEvent>(
            owner,
            context =>
            {
                handler(context);

                return ValueTask.CompletedTask;
            },
            options);
    }

    public IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        IEventHandler<TEvent> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Subscribe<TEvent>(
            owner,
            context => handler.HandleAsync(context),
            options);
    }

    public IEventSubscription Subscribe<TEvent>(Func<TEvent, CancellationToken, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Subscribe<TEvent>(
            context => handler(context.Event, context.CancellationToken),
            EventSubscriptionOptions.Serialized);
    }

    public async ValueTask<EventPublishResult> PublishAsync<TEvent>(
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        return await PublishCoreAsync(
                eventData,
                options,
                Guid.NewGuid(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<EventPublishResult> PublishCoreAsync<TEvent>(
        TEvent eventData,
        EventPublishOptions? options,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var publishOptions = NormalizeAndValidatePublishOptions(options);
        var descriptor = _contractRegistry.GetOrCreate<TEvent>();
        var publishedAt = DateTimeOffset.UtcNow;
        var correlationId = string.IsNullOrWhiteSpace(publishOptions.CorrelationId)
            ? eventId.ToString("D")
            : publishOptions.CorrelationId;
        var snapshot = GetSnapshot(typeof(TEvent));
        var deliveries = new List<EventDeliveryResult>(snapshot.Length);

        WriteDiagnostic(
            EventDiagnosticIds.EventPublished,
            $"Event '{descriptor.ContractId.Value}' with id '{eventId:D}' was published.",
            HostDiagnosticSeverity.Trace,
            CreateEventDiagnosticContext(descriptor, eventId));

        foreach (var subscription in snapshot)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (deliveries.Count > 0)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            var delivery = await subscription.DeliverAsync(
                    eventData!,
                    descriptor,
                    eventId,
                    correlationId,
                    publishOptions.CausationId,
                    publishedAt,
                    publishOptions.PublishDepth,
                    cancellationToken)
                .ConfigureAwait(false);

            if (delivery is null)
            {
                continue;
            }

            deliveries.Add(delivery);

            if (delivery.Canceled)
            {
                break;
            }

            if (!delivery.Succeeded
                && subscription.Options.ErrorPolicy == EventErrorPolicy.StopPublication)
            {
                break;
            }
        }

        return new EventPublishResult(
            eventId,
            descriptor.ContractId,
            deliveries);
    }

    public ValueTask<EventPostResult> PostAsync<TEvent>(
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        ThrowIfDisposed();
        var publishOptions = NormalizeAndValidatePublishOptions(options);

        var descriptor = _contractRegistry.GetOrCreate<TEvent>();
        var eventId = Guid.NewGuid();

        if (cancellationToken.IsCancellationRequested)
        {
            WriteDiagnostic(
                EventDiagnosticIds.EventRejected,
                $"Posted event '{descriptor.ContractId.Value}' with id '{eventId:D}' was rejected because publication was canceled before acceptance.",
                HostDiagnosticSeverity.Warning,
                CreateEventDiagnosticContext(descriptor, eventId));

            return ValueTask.FromResult(
                new EventPostResult(
                    eventId,
                    descriptor.ContractId,
                    Accepted: false,
                    "Publication was canceled before it was accepted."));
        }

        WriteDiagnostic(
            EventDiagnosticIds.EventAccepted,
            $"Posted event '{descriptor.ContractId.Value}' with id '{eventId:D}' was accepted.",
            HostDiagnosticSeverity.Trace,
            CreateEventDiagnosticContext(descriptor, eventId));

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await PublishCoreAsync(
                            eventData,
                            publishOptions,
                            eventId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    var context = CreatePostedFailureDiagnosticContext(descriptor, eventId, exception);
                    var subscriptionMessage = context.TryGetValue(SubscriptionIdContextKey, out var subscriptionId) &&
                                              !string.IsNullOrWhiteSpace(subscriptionId)
                        ? $" subscription '{subscriptionId}'"
                        : string.Empty;

                    WriteDiagnostic(
                        EventDiagnosticIds.EventDeliveryFailed,
                        $"Posted event '{descriptor.ContractId.Value}' with id '{eventId:D}'{subscriptionMessage} failed: {exception.Message}",
                        HostDiagnosticSeverity.Error,
                        context);
                }
            },
            CancellationToken.None);

        return ValueTask.FromResult(
            new EventPostResult(
                eventId,
                descriptor.ContractId,
                Accepted: true));
    }

    public void Dispose()
    {
        EventSubscription[] subscriptions;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            subscriptions = _subscriptions.Values
                .SelectMany(group => group)
                .ToArray();
            _subscriptions.Clear();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }
    }

    private IEventSubscription SubscribeCore<TEvent>(
        LifecycleScope? owner,
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        var subscription = new EventSubscription(
            this,
            typeof(TEvent),
            options,
            (eventData, delivery) =>
            {
                var context = new EventContext<TEvent>(
                    (TEvent)eventData,
                    delivery.ContractId,
                    delivery.EventId,
                    delivery.CorrelationId,
                    delivery.CausationId,
                    delivery.PublishedAt,
                    delivery.PublishDepth,
                    delivery.SubscriptionId,
                    delivery.DispatchPolicy,
                    delivery.CancellationToken);

                return handler(context);
            },
            _diagnostics,
            owner);

        lock (_syncRoot)
        {
            if (!_subscriptions.TryGetValue(subscription.EventType, out var subscriptions))
            {
                subscriptions = [];
                _subscriptions[subscription.EventType] = subscriptions;
            }

            subscriptions.Add(subscription);
            subscription.MarkActive();
        }

        WriteDiagnostic(
            EventDiagnosticIds.EventSubscriptionAdded,
            $"Event subscription '{subscription.Id}' was added.",
            HostDiagnosticSeverity.Trace,
            CreateSubscriptionDiagnosticContext(subscription));

        return subscription;
    }

    private static EventPublishOptions NormalizeAndValidatePublishOptions(EventPublishOptions? options)
    {
        var publishOptions = options ?? EventPublishOptions.Default;
        if (publishOptions.PublishDepth < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EventPublishOptions.PublishDepth),
                publishOptions.PublishDepth,
                "Event publish depth cannot be negative.");
        }

        return publishOptions;
    }

    private EventSubscription[] GetSnapshot(Type eventType)
    {
        lock (_syncRoot)
        {
            return _subscriptions.TryGetValue(eventType, out var subscriptions)
                ? subscriptions
                    .Where(subscription => subscription.State == EventSubscriptionState.Active)
                    .ToArray()
                : [];
        }
    }

    private void Remove(EventSubscription subscription)
    {
        lock (_syncRoot)
        {
            if (!_subscriptions.TryGetValue(subscription.EventType, out var subscriptions))
            {
                return;
            }

            subscriptions.Remove(subscription);
            if (subscriptions.Count == 0)
            {
                _subscriptions.Remove(subscription.EventType);
            }
        }

        WriteDiagnostic(
            EventDiagnosticIds.EventSubscriptionDisposed,
            $"Event subscription '{subscription.Id}' was disposed.",
            HostDiagnosticSeverity.Trace,
            CreateSubscriptionDiagnosticContext(subscription));
    }

    private void WriteDiagnostic(
        string code,
        string message,
        HostDiagnosticSeverity severity,
        IReadOnlyDictionary<string, string?>? context = null)
    {
        var record = new HostDiagnosticRecord(code, message, severity);
        if (context is not null)
        {
            record = record with { Context = context };
        }

        _diagnostics?.Write(record);
    }

    private static IReadOnlyDictionary<string, string?> CreateEventDiagnosticContext(
        EventContractDescriptor descriptor,
        Guid eventId)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [ContractIdContextKey] = descriptor.ContractId.Value,
            [EventIdContextKey] = eventId.ToString("D"),
            [EventTypeContextKey] = descriptor.EventType.FullName
        };
    }

    private static IReadOnlyDictionary<string, string?> CreateSubscriptionDiagnosticContext(
        EventSubscription subscription)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [SubscriptionIdContextKey] = subscription.Id.ToString(),
            [EventTypeContextKey] = subscription.EventType.FullName,
            [DispatchPolicyContextKey] = subscription.Options.DispatchPolicy.ToString(),
            [ErrorPolicyContextKey] = subscription.Options.ErrorPolicy.ToString()
        };
    }

    private static IReadOnlyDictionary<string, string?> CreateDeliveryDiagnosticContext(
        EventContractDescriptor descriptor,
        Guid eventId,
        EventSubscriptionId subscriptionId,
        EventSubscriptionOptions options)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [ContractIdContextKey] = descriptor.ContractId.Value,
            [EventIdContextKey] = eventId.ToString("D"),
            [SubscriptionIdContextKey] = subscriptionId.ToString(),
            [EventTypeContextKey] = descriptor.EventType.FullName,
            [DispatchPolicyContextKey] = options.DispatchPolicy.ToString(),
            [ErrorPolicyContextKey] = options.ErrorPolicy.ToString()
        };
    }

    private static IReadOnlyDictionary<string, string?> CreatePostedFailureDiagnosticContext(
        EventContractDescriptor descriptor,
        Guid eventId,
        Exception exception)
    {
        var context = new Dictionary<string, string?>(
            CreateEventDiagnosticContext(descriptor, eventId),
            StringComparer.Ordinal);

        if (exception.Data[DeliveryExceptionSubscriptionIdDataKey] is string subscriptionId)
        {
            context[SubscriptionIdContextKey] = subscriptionId;
        }

        if (exception.Data[DeliveryExceptionContractIdDataKey] is string contractId)
        {
            context[ContractIdContextKey] = contractId;
        }

        if (exception.Data[DeliveryExceptionEventIdDataKey] is string deliveredEventId)
        {
            context[EventIdContextKey] = deliveredEventId;
        }

        return context;
    }

    private static void AttachDeliveryContextToException(
        Exception exception,
        EventContractId contractId,
        Guid eventId,
        EventSubscriptionId subscriptionId)
    {
        exception.Data[DeliveryExceptionContractIdDataKey] = contractId.Value;
        exception.Data[DeliveryExceptionEventIdDataKey] = eventId.ToString("D");
        exception.Data[DeliveryExceptionSubscriptionIdDataKey] = subscriptionId.ToString();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private static void ThrowIfOwnerNotRunning(LifecycleScope owner)
    {
        if (owner.State != LifecycleScopeState.Running)
        {
            throw new InvalidOperationException("Event subscription owner scope must be running.");
        }
    }

    private readonly record struct DeliveryContext(
        EventContractId ContractId,
        Guid EventId,
        string CorrelationId,
        string? CausationId,
        DateTimeOffset PublishedAt,
        int PublishDepth,
        EventSubscriptionId SubscriptionId,
        EventDispatchPolicy DispatchPolicy,
        CancellationToken CancellationToken);

    private sealed class EventSubscription : IEventSubscription
    {
        private readonly InMemoryEventBus _eventBus;
        private readonly Func<object, DeliveryContext, ValueTask> _handler;
        private readonly IHostDiagnostics? _diagnostics;
        private readonly SemaphoreSlim _serialGate = new(1, 1);
        private readonly object _stateGate = new();
        private readonly CancellationTokenRegistration _ownerCancellation;
        private TaskCompletionSource? _drainCompletion;
        private int _inFlightCount;
        private EventSubscriptionState _state = EventSubscriptionState.Created;

        public EventSubscription(
            InMemoryEventBus eventBus,
            Type eventType,
            EventSubscriptionOptions options,
            Func<object, DeliveryContext, ValueTask> handler,
            IHostDiagnostics? diagnostics,
            LifecycleScope? owner)
        {
            _eventBus = eventBus;
            EventType = eventType;
            Options = options;
            _handler = handler;
            _diagnostics = diagnostics;
            Id = EventSubscriptionId.New();

            if (owner is not null)
            {
                _ownerCancellation = owner.CancellationToken.Register(
                    static state => ((EventSubscription)state!).Dispose(),
                    this);
            }
        }

        public EventSubscriptionId Id { get; }

        public Type EventType { get; }

        public EventSubscriptionOptions Options { get; }

        public EventSubscriptionState State
        {
            get
            {
                lock (_stateGate)
                {
                    return _state;
                }
            }
        }

        public void MarkActive()
        {
            lock (_stateGate)
            {
                if (_state == EventSubscriptionState.Created)
                {
                    _state = EventSubscriptionState.Active;
                }
            }
        }

        public async ValueTask<EventDeliveryResult?> DeliverAsync(
            object eventData,
            EventContractDescriptor descriptor,
            Guid eventId,
            string correlationId,
            string? causationId,
            DateTimeOffset publishedAt,
            int publishDepth,
            CancellationToken cancellationToken)
        {
            if (State != EventSubscriptionState.Active)
            {
                return null;
            }

            if (Options.DispatchPolicy == EventDispatchPolicy.Serialized)
            {
                await _serialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                var acquiredDelivery = false;

                try
                {
                    acquiredDelivery = TryBeginDelivery();
                    if (!acquiredDelivery)
                    {
                        return null;
                    }

                    return await DispatchAsync(
                            eventData,
                            descriptor,
                            eventId,
                            correlationId,
                            causationId,
                            publishedAt,
                            publishDepth,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    if (acquiredDelivery)
                    {
                        EndDelivery();
                    }

                    _serialGate.Release();
                }
            }

            if (!TryBeginDelivery())
            {
                return null;
            }

            try
            {
                return await DispatchAsync(
                        eventData,
                        descriptor,
                        eventId,
                        correlationId,
                        causationId,
                        publishedAt,
                        publishDepth,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                EndDelivery();
            }
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            lock (_stateGate)
            {
                if (_state == EventSubscriptionState.Disposed)
                {
                    return;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            Task? drainTask;
            var removeFromBus = false;

            lock (_stateGate)
            {
                if (_state == EventSubscriptionState.Disposed)
                {
                    return;
                }

                if (_state != EventSubscriptionState.Quiescing)
                {
                    _state = EventSubscriptionState.Quiescing;
                    removeFromBus = true;
                }

                if (_inFlightCount > 0)
                {
                    _drainCompletion ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    drainTask = _drainCompletion.Task;
                }
                else
                {
                    drainTask = null;
                }
            }

            if (removeFromBus)
            {
                _eventBus.Remove(this);
                _ownerCancellation.Dispose();
            }

            if (drainTask is not null)
            {
                await drainTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            lock (_stateGate)
            {
                _state = EventSubscriptionState.Disposed;
            }
        }

        public void Dispose()
        {
            StopAsync().AsTask().GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync()
        {
            return StopAsync();
        }

        private async ValueTask<EventDeliveryResult> DispatchAsync(
            object eventData,
            EventContractDescriptor descriptor,
            Guid eventId,
            string correlationId,
            string? causationId,
            DateTimeOffset publishedAt,
            int publishDepth,
            CancellationToken cancellationToken)
        {
            try
            {
                var delivery = new DeliveryContext(
                    descriptor.ContractId,
                    eventId,
                    correlationId,
                    causationId,
                    publishedAt,
                    publishDepth,
                    Id,
                    Options.DispatchPolicy,
                    cancellationToken);

                await DispatchCoreAsync(eventData, delivery, cancellationToken).ConfigureAwait(false);

                return new EventDeliveryResult(
                    Id,
                    Options.DispatchPolicy,
                    Succeeded: true);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                _diagnostics?.Write(
                    new HostDiagnosticRecord(
                        EventDiagnosticIds.EventDeliveryCancelled,
                        $"Event handler '{Id}' was cancelled for contract '{descriptor.ContractId.Value}' event '{eventId:D}' subscription '{Id}': {exception.Message}",
                        HostDiagnosticSeverity.Trace)
                    {
                        Context = CreateDeliveryDiagnosticContext(descriptor, eventId, Id, Options)
                    });

                return new EventDeliveryResult(
                    Id,
                    Options.DispatchPolicy,
                    Succeeded: false,
                    exception.Message,
                    Canceled: true);
            }
            catch (Exception exception)
            {
                _diagnostics?.Write(
                    new HostDiagnosticRecord(
                        EventDiagnosticIds.EventDeliveryFailed,
                        $"Event handler '{Id}' failed for contract '{descriptor.ContractId.Value}' event '{eventId:D}' subscription '{Id}': {exception.Message}",
                        HostDiagnosticSeverity.Error)
                    {
                        Context = CreateDeliveryDiagnosticContext(descriptor, eventId, Id, Options)
                    });

                if (Options.ErrorPolicy == EventErrorPolicy.FailPublisher)
                {
                    AttachDeliveryContextToException(exception, descriptor.ContractId, eventId, Id);

                    throw;
                }

                return new EventDeliveryResult(
                    Id,
                    Options.DispatchPolicy,
                    Succeeded: false,
                    exception.Message);
            }
        }

        private async ValueTask DispatchCoreAsync(
            object eventData,
            DeliveryContext delivery,
            CancellationToken cancellationToken)
        {
            switch (Options.DispatchPolicy)
            {
                case EventDispatchPolicy.UiThread:
                    if (Options.UiDispatcher is null)
                    {
                        throw new InvalidOperationException("UI dispatcher subscription requires a dispatcher.");
                    }

                    await Options.UiDispatcher.PostAsync(
                            async token => await _handler(eventData, delivery with { CancellationToken = token }).ConfigureAwait(false),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case EventDispatchPolicy.Background:
                    await Task.Run(
                            async () => await _handler(eventData, delivery).ConfigureAwait(false),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                default:
                    await _handler(eventData, delivery).ConfigureAwait(false);
                    break;
            }
        }

        private bool TryBeginDelivery()
        {
            lock (_stateGate)
            {
                if (_state != EventSubscriptionState.Active)
                {
                    return false;
                }

                _inFlightCount++;

                return true;
            }
        }

        private void EndDelivery()
        {
            TaskCompletionSource? drainCompletion = null;

            lock (_stateGate)
            {
                _inFlightCount--;

                if (_inFlightCount == 0 && _state == EventSubscriptionState.Quiescing)
                {
                    drainCompletion = _drainCompletion;
                }
            }

            drainCompletion?.TrySetResult();
        }
    }
}
