using AtomUI.City.Data;

namespace AtomUI.City.Data.Tests;

public sealed class DataConnectionLifecycleTests
{
    [Fact]
    public async Task ConnectionManagerStartsAndStopsConnection()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "sales-plugin");
        var connection = new RecordingConnection("sales-hub", owner);
        var manager = new DataConnectionManager();

        manager.Register(connection);
        await manager.StartOwnerAsync(owner);
        await manager.StopOwnerAsync(owner);

        Assert.Equal(DataConnectionState.Stopped, connection.State);
        Assert.Equal(1, connection.StartCount);
        Assert.Equal(1, connection.StopCount);
    }

    [Fact]
    public async Task ConnectionManagerWritesStartedDiagnostic()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "sales-plugin");
        var connection = new RecordingConnection("sales-hub", owner);
        var manager = new DataConnectionManager(diagnostics);
        manager.Register(connection);

        await manager.StartOwnerAsync(owner);

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == DataDiagnosticIds.ConnectionStarted);
        Assert.Equal(DataDiagnosticSeverity.Info, record.Severity);
        Assert.Contains("sales-hub", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectionManagerRejectsOwnerlessLongRunningConnection()
    {
        var connection = new RecordingConnection(
            "manual-hub",
            DataConnectionOwner.None);
        var manager = new DataConnectionManager();

        var result = manager.Register(connection);

        Assert.False(result.Succeeded);
        Assert.Equal(DataErrorKind.PolicyRejected, result.Error?.Kind);
        await manager.StopAllAsync();
        Assert.Equal(0, connection.StopCount);
    }

    [Fact]
    public void ConnectionManagerWritesRejectedRegistrationDiagnostic()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var connection = new RecordingConnection(
            "manual-hub",
            DataConnectionOwner.None);
        var manager = new DataConnectionManager(diagnostics);

        manager.Register(connection);

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == DataDiagnosticIds.ConnectionRegistrationRejected);
        Assert.Equal(DataDiagnosticSeverity.Warning, record.Severity);
        Assert.Equal(DataErrorKind.PolicyRejected, record.ErrorKind);
        Assert.Contains("manual-hub", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectionManagerWritesRegisteredDiagnostic()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "sales-plugin");
        var connection = new RecordingConnection("sales-hub", owner);
        var manager = new DataConnectionManager(diagnostics);

        manager.Register(connection);

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == DataDiagnosticIds.ConnectionRegistered);
        Assert.Contains("sales-hub", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectionManagerWritesStartFailureDiagnosticAndPropagatesFailure()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "sales-plugin");
        var connection = new RecordingConnection("sales-hub", owner)
        {
            StartFailure = new InvalidOperationException("start failed"),
        };
        var manager = new DataConnectionManager(diagnostics);
        manager.Register(connection);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.StartOwnerAsync(owner).AsTask());

        Assert.Equal("start failed", exception.Message);
        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == DataDiagnosticIds.ConnectionStartFailed);
        Assert.Equal(DataDiagnosticSeverity.Error, record.Severity);
        Assert.Equal(DataErrorKind.ConnectionFailed, record.ErrorKind);
        Assert.Contains("sales-hub", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectionManagerWritesStoppedDiagnostic()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "sales-plugin");
        var connection = new RecordingConnection("sales-hub", owner);
        var manager = new DataConnectionManager(diagnostics);
        manager.Register(connection);

        await manager.StopOwnerAsync(owner);

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == DataDiagnosticIds.ConnectionStopped);
        Assert.Contains("sales-hub", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectionManagerDoesNotStopAlreadyStoppedConnectionTwice()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "sales-plugin");
        var connection = new RecordingConnection("sales-hub", owner);
        var manager = new DataConnectionManager();
        manager.Register(connection);

        await manager.StopOwnerAsync(owner);
        await manager.StopOwnerAsync(owner);

        Assert.Equal(DataConnectionState.Stopped, connection.State);
        Assert.Equal(1, connection.StopCount);
    }

    [Fact]
    public async Task ConnectionManagerWritesStopFailureDiagnosticAndPropagatesFailure()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "sales-plugin");
        var connection = new RecordingConnection("sales-hub", owner)
        {
            StopFailure = new InvalidOperationException("stop failed"),
        };
        var manager = new DataConnectionManager(diagnostics);
        manager.Register(connection);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.StopOwnerAsync(owner).AsTask());

        Assert.Equal("stop failed", exception.Message);
        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == DataDiagnosticIds.ConnectionStopFailed);
        Assert.Equal(DataDiagnosticSeverity.Error, record.Severity);
        Assert.Equal(DataErrorKind.ConnectionFailed, record.ErrorKind);
        Assert.Contains("sales-hub", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentOwnerStartAndStopInvokeConnectionOnce()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "concurrent-plugin");
        var connection = new RecordingConnection("concurrent-hub", owner);
        var manager = new DataConnectionManager();
        manager.Register(connection);

        await Task.WhenAll(Enumerable.Range(0, 64)
            .Select(_ => manager.StartOwnerAsync(owner).AsTask()));
        await Task.WhenAll(Enumerable.Range(0, 64)
            .Select(_ => manager.StopOwnerAsync(owner).AsTask()));

        Assert.Equal(1, connection.StartCount);
        Assert.Equal(1, connection.StopCount);
    }

    [Fact]
    public async Task StopOwnerContinuesAfterEarlierConnectionFailure()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "cleanup-plugin");
        var healthy = new RecordingConnection("healthy-hub", owner);
        var failing = new RecordingConnection("failing-hub", owner)
        {
            StopFailure = new InvalidOperationException("stop failed"),
        };
        var manager = new DataConnectionManager();
        manager.Register(healthy);
        manager.Register(failing);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StopOwnerAsync(owner).AsTask());

        Assert.Equal(1, failing.StopCount);
        Assert.Equal(1, healthy.StopCount);
        Assert.Equal(DataConnectionState.Stopped, healthy.State);
    }

    [Fact]
    public async Task StopOwnerAggregatesMultipleConnectionFailures()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "aggregate-plugin");
        var firstFailure = new InvalidOperationException("first stop failed");
        var secondFailure = new NotSupportedException("second stop failed");
        var manager = new DataConnectionManager();
        manager.Register(new RecordingConnection("first-hub", owner) { StopFailure = firstFailure });
        manager.Register(new RecordingConnection("second-hub", owner) { StopFailure = secondFailure });

        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => manager.StopOwnerAsync(owner).AsTask());

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Contains(firstFailure, exception.InnerExceptions);
        Assert.Contains(secondFailure, exception.InnerExceptions);
    }

    [Fact]
    public async Task StartOwnerRollsBackConnectionsStartedBeforeFailure()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "rollback-plugin");
        var started = new RecordingConnection("started-hub", owner);
        var failing = new RecordingConnection("failing-hub", owner)
        {
            StartFailure = new InvalidOperationException("start failed"),
        };
        var manager = new DataConnectionManager();
        manager.Register(started);
        manager.Register(failing);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartOwnerAsync(owner).AsTask());

        Assert.Equal(1, started.StartCount);
        Assert.Equal(1, started.StopCount);
        Assert.Equal(DataConnectionState.Stopped, started.State);
    }

    [Fact]
    public async Task RegistrationRevokeStopsAndRemovesConnection()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Manual, "manual-owner");
        var connection = new RecordingConnection("manual-hub", owner);
        var manager = new DataConnectionManager();
        var registration = manager.Register(connection);
        Assert.True(registration.Succeeded);

        await registration.Value!.RevokeAsync();
        await registration.Value.RevokeAsync();
        var replacement = new RecordingConnection("manual-hub", owner);
        var replacementRegistration = manager.Register(replacement);

        Assert.Equal(1, connection.StopCount);
        Assert.True(replacementRegistration.Succeeded);
    }

    [Fact]
    public async Task ConcurrentRegistrationRevokesWaitForSameStopTransaction()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Manual, "manual-owner");
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new BlockingStopConnection("manual-hub", owner, stopStarted, releaseStop);
        var manager = new DataConnectionManager();
        var registration = manager.Register(connection).Value!;

        var firstRevoke = registration.RevokeAsync().AsTask();
        await stopStarted.Task;
        var secondRevoke = registration.RevokeAsync().AsTask();

        Assert.False(firstRevoke.IsCompleted);
        Assert.False(secondRevoke.IsCompleted);
        releaseStop.SetResult();
        await Task.WhenAll(firstRevoke, secondRevoke);

        Assert.Equal(1, connection.StopCount);
    }

    [Fact]
    public async Task ConcurrentStartObservesCancellationWhileWaitingForConnectionGate()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Manual, "manual-owner");
        var startEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new BlockingStartConnection("manual-hub", owner, startEntered, releaseStart);
        var manager = new DataConnectionManager();
        Assert.True(manager.Register(connection).Succeeded);

        var firstStart = manager.StartOwnerAsync(owner).AsTask();
        await startEntered.Task;
        using var cancellation = new CancellationTokenSource();
        var cancelledStart = manager.StartOwnerAsync(owner, cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        var completed = await Task.WhenAny(cancelledStart, Task.Delay(TimeSpan.FromSeconds(1)));
        releaseStart.SetResult();
        await firstStart;

        Assert.Same(cancelledStart, completed);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledStart);
        Assert.Equal(1, connection.StartCount);
    }

    [Fact]
    public async Task StoppedOwnerRejectsNewConnection()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Route, "route:/closed");
        var manager = new DataConnectionManager();
        await manager.StopOwnerAsync(owner);

        var result = manager.Register(new RecordingConnection("late-hub", owner));

        Assert.False(result.Succeeded);
        Assert.Equal(DataErrorKind.PolicyRejected, result.Error?.Kind);
    }

    [Fact]
    public async Task ConnectionCallbacksRunOutsideLifecycleSynchronizationAndReentryFailsFast()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Manual, "reentrant-owner");
        var manager = new DataConnectionManager();
        var connection = new ReentrantStartConnection(
            "reentrant-hub",
            owner,
            () => manager.StopOwnerAsync(owner));
        Assert.True(manager.Register(connection).Succeeded);

        var startTask = manager.StartOwnerAsync(owner).AsTask();
        var completed = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(startTask, completed);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => startTask);
        Assert.Contains("cannot execute 'stop' recursively", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedRegistrationRevokeCanBeRetried()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Manual, "retry-owner");
        var connection = new FailOnceStopConnection("retry-hub", owner);
        var manager = new DataConnectionManager();
        var registration = manager.Register(connection).Value!;

        await Assert.ThrowsAsync<InvalidOperationException>(() => registration.RevokeAsync().AsTask());
        await registration.RevokeAsync();

        Assert.Equal(2, connection.StopCount);
        Assert.True(manager.Register(new RecordingConnection("retry-hub", owner)).Succeeded);
    }

    [Fact]
    public async Task StopOwnerUsesExplicitReverseRegistrationOrder()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Manual, "ordered-owner");
        var stopOrder = new List<string>();
        var manager = new DataConnectionManager();
        manager.Register(new OrderedStopConnection("first", owner, stopOrder));
        manager.Register(new OrderedStopConnection("second", owner, stopOrder));
        manager.Register(new OrderedStopConnection("third", owner, stopOrder));

        await manager.StopOwnerAsync(owner);

        Assert.Equal(["third", "second", "first"], stopOrder);
    }

    [Fact]
    public async Task PreCancelledOwnerStopThrowsCancellationWithoutAggregating()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Manual, "cancelled-owner");
        var first = new RecordingConnection("first", owner);
        var second = new RecordingConnection("second", owner);
        var manager = new DataConnectionManager();
        manager.Register(first);
        manager.Register(second);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.StopOwnerAsync(owner, cancellation.Token).AsTask());

        Assert.IsNotType<AggregateException>(exception);
        Assert.Equal(0, first.StopCount);
        Assert.Equal(0, second.StopCount);
    }

    [Fact]
    public void DuplicateConnectionIdIsRejected()
    {
        var owner = new DataConnectionOwner(DataConnectionOwnerKind.Plugin, "duplicate-plugin");
        var manager = new DataConnectionManager();
        Assert.True(manager.Register(new RecordingConnection("shared-hub", owner)).Succeeded);

        var duplicate = manager.Register(new RecordingConnection("shared-hub", owner));

        Assert.False(duplicate.Succeeded);
        Assert.Equal(DataErrorKind.PolicyRejected, duplicate.Error?.Kind);
    }

    private sealed class RecordingConnection : IDataConnection
    {
        public RecordingConnection(string connectionId, DataConnectionOwner owner)
        {
            ConnectionId = connectionId;
            Owner = owner;
        }

        public string ConnectionId { get; }

        public DataConnectionOwner Owner { get; }

        public DataConnectionState State { get; private set; } = DataConnectionState.Created;

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public Exception? StartFailure { get; init; }

        public Exception? StopFailure { get; init; }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            if (StartFailure is not null)
            {
                throw StartFailure;
            }

            State = DataConnectionState.Connected;

            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            if (StopFailure is not null)
            {
                throw StopFailure;
            }

            State = DataConnectionState.Stopped;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingStopConnection : IDataConnection
    {
        private readonly TaskCompletionSource _stopStarted;
        private readonly TaskCompletionSource _releaseStop;

        public BlockingStopConnection(
            string connectionId,
            DataConnectionOwner owner,
            TaskCompletionSource stopStarted,
            TaskCompletionSource releaseStop)
        {
            ConnectionId = connectionId;
            Owner = owner;
            _stopStarted = stopStarted;
            _releaseStop = releaseStop;
        }

        public string ConnectionId { get; }

        public DataConnectionOwner Owner { get; }

        public DataConnectionState State { get; private set; } = DataConnectionState.Created;

        public int StopCount { get; private set; }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            State = DataConnectionState.Connected;
            return ValueTask.CompletedTask;
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            _stopStarted.TrySetResult();
            await _releaseStop.Task.WaitAsync(cancellationToken);
            State = DataConnectionState.Stopped;
        }
    }

    private sealed class BlockingStartConnection : IDataConnection
    {
        private readonly TaskCompletionSource _startEntered;
        private readonly TaskCompletionSource _releaseStart;

        public BlockingStartConnection(
            string connectionId,
            DataConnectionOwner owner,
            TaskCompletionSource startEntered,
            TaskCompletionSource releaseStart)
        {
            ConnectionId = connectionId;
            Owner = owner;
            _startEntered = startEntered;
            _releaseStart = releaseStart;
        }

        public string ConnectionId { get; }

        public DataConnectionOwner Owner { get; }

        public DataConnectionState State { get; private set; } = DataConnectionState.Created;

        public int StartCount { get; private set; }

        public async ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            _startEntered.TrySetResult();
            await _releaseStart.Task.WaitAsync(cancellationToken);
            State = DataConnectionState.Connected;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            State = DataConnectionState.Stopped;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ReentrantStartConnection : IDataConnection
    {
        private readonly Func<ValueTask> _stopOwner;

        public ReentrantStartConnection(
            string connectionId,
            DataConnectionOwner owner,
            Func<ValueTask> stopOwner)
        {
            ConnectionId = connectionId;
            Owner = owner;
            _stopOwner = stopOwner;
        }

        public string ConnectionId { get; }

        public DataConnectionOwner Owner { get; }

        public DataConnectionState State { get; private set; } = DataConnectionState.Created;

        public async ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            await _stopOwner();
            State = DataConnectionState.Connected;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            State = DataConnectionState.Stopped;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailOnceStopConnection : IDataConnection
    {
        public FailOnceStopConnection(string connectionId, DataConnectionOwner owner)
        {
            ConnectionId = connectionId;
            Owner = owner;
        }

        public string ConnectionId { get; }

        public DataConnectionOwner Owner { get; }

        public DataConnectionState State { get; private set; } = DataConnectionState.Created;

        public int StopCount { get; private set; }

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            State = DataConnectionState.Connected;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            if (StopCount == 1)
            {
                throw new InvalidOperationException("transient stop failure");
            }

            State = DataConnectionState.Stopped;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class OrderedStopConnection : IDataConnection
    {
        private readonly ICollection<string> _stopOrder;

        public OrderedStopConnection(
            string connectionId,
            DataConnectionOwner owner,
            ICollection<string> stopOrder)
        {
            ConnectionId = connectionId;
            Owner = owner;
            _stopOrder = stopOrder;
        }

        public string ConnectionId { get; }

        public DataConnectionOwner Owner { get; }

        public DataConnectionState State { get; private set; } = DataConnectionState.Created;

        public ValueTask StartAsync(CancellationToken cancellationToken = default)
        {
            State = DataConnectionState.Connected;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            _stopOrder.Add(ConnectionId);
            State = DataConnectionState.Stopped;
            return ValueTask.CompletedTask;
        }
    }
}
