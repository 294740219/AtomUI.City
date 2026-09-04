using System.Collections.Concurrent;
using AtomUI.City.Core.Threading;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventDispatchAndFailurePolicyTests
{
    [Fact]
    public async Task UiPublicationWaitsForDeferredDispatcherCallbackCompletion()
    {
        var dispatcher = new DeferredUiDispatcher();
        await using var eventBus = new InMemoryEventBus();
        var handlerCompleted = false;

        eventBus.Subscribe<TestEvent>(
            async _ =>
            {
                await Task.Yield();
                handlerCompleted = true;
            },
            EventSubscriptionOptions.UiThread(dispatcher));

        var publication = eventBus.PublishAsync(new TestEvent()).AsTask();
        await dispatcher.Posted;

        Assert.False(publication.IsCompleted);
        Assert.False(handlerCompleted);

        await dispatcher.ExecuteNextAsync();
        var result = await publication;

        Assert.True(result.Succeeded);
        Assert.True(handlerCompleted);
    }

    [Fact]
    public async Task BackgroundDispatchUsesInjectedManagedScheduler()
    {
        var scheduler = new RecordingBackgroundScheduler();
        await using var eventBus = new InMemoryEventBus(backgroundScheduler: scheduler);

        eventBus.Subscribe<TestEvent>(
            _ => ValueTask.CompletedTask,
            EventSubscriptionOptions.Background());

        var result = await eventBus.PublishAsync(new TestEvent());

        Assert.True(result.Succeeded);
        Assert.Equal(1, scheduler.RunCount);
    }

    [Fact]
    public async Task CurrentDispatchDoesNotLeakRuntimeCreatorExecutionContext()
    {
        var ambient = new AsyncLocal<string?>();
        ambient.Value = "publisher-secret";
        await using var eventBus = new InMemoryEventBus();
        string? observed = "not-called";

        eventBus.Subscribe<TestEvent>(context =>
        {
            observed = ambient.Value;
            return ValueTask.CompletedTask;
        }, EventSubscriptionOptions.Current);

        var result = await eventBus.PublishAsync(new TestEvent());

        Assert.True(result.Succeeded);
        Assert.Null(observed);
    }

    [Fact]
    public async Task UiInlineModeUsesDispatcherAccessCheckWithoutPosting()
    {
        var dispatcher = new InlineUiDispatcher();
        await using var eventBus = new InMemoryEventBus();
        var called = false;

        eventBus.Subscribe<TestEvent>(
            _ =>
            {
                called = true;
                return ValueTask.CompletedTask;
            },
            EventSubscriptionOptions.UiThread(
                dispatcher,
                EventDispatchMode.InlineIfAllowed));

        var result = await eventBus.PublishAsync(new TestEvent());

        Assert.True(result.Succeeded);
        Assert.True(called);
        Assert.Equal(0, dispatcher.PostCount);
    }

    [Fact]
    public async Task DispatcherRejectionBecomesDeliveryFailure()
    {
        await using var eventBus = new InMemoryEventBus();
        eventBus.Subscribe<TestEvent>(
            _ => ValueTask.CompletedTask,
            EventSubscriptionOptions.UiThread(new UnavailableUiDispatcher()));

        var result = await eventBus.PublishAsync(new TestEvent());

        var delivery = Assert.Single(result.Deliveries);
        Assert.Equal(EventDeliveryStatus.Failed, delivery.Status);
        Assert.Contains(HostDiagnosticIds.DispatcherUnavailable, delivery.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeferredUiHandlerAsyncFailureIsObservedByPublication()
    {
        var dispatcher = new DeferredUiDispatcher();
        await using var eventBus = new InMemoryEventBus();
        eventBus.Subscribe<TestEvent>(
            async _ =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async-ui-boom");
            },
            EventSubscriptionOptions.UiThread(dispatcher));

        var publication = eventBus.PublishAsync(new TestEvent()).AsTask();
        await dispatcher.Posted;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await dispatcher.ExecuteNextAsync());
        var result = await publication;

        var delivery = Assert.Single(result.Deliveries);
        Assert.Equal(EventDeliveryStatus.Failed, delivery.Status);
        Assert.Equal("async-ui-boom", delivery.ErrorMessage);
    }

    [Fact]
    public async Task HandlerTimeoutReturnsWithoutPretendingLingeringHandlerHasDrained()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        await using var eventBus = new InMemoryEventBus(diagnostics: diagnostics);
        var handlerStarted = NewCompletion();
        var releaseHandler = NewCompletion();
        var subscription = eventBus.Subscribe<TestEvent>(
            async _ =>
            {
                handlerStarted.TrySetResult();
                await releaseHandler.Task;
            },
            EventSubscriptionOptions.Serialized.WithHandlerTimeout(TimeSpan.FromMilliseconds(50)));

        var publication = eventBus.PublishAsync(new TestEvent()).AsTask();
        await handlerStarted.Task;
        var result = await publication.WaitAsync(TimeSpan.FromSeconds(5));

        var delivery = Assert.Single(result.Deliveries);
        Assert.Equal(EventDeliveryStatus.TimedOut, delivery.Status);
        Assert.Equal(1, result.TimedOutCount);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == EventDiagnosticIds.EventDeliveryTimedOut);

        var stop = subscription.StopAsync().AsTask();
        Assert.False(stop.IsCompleted);

        releaseHandler.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
    }

    [Fact]
    public async Task IndependentSubscriptionsCanRunConcurrentlyWithinOnePublication()
    {
        await using var eventBus = new InMemoryEventBus();
        var firstStarted = NewCompletion();
        var secondStarted = NewCompletion();
        var release = NewCompletion();

        eventBus.Subscribe<TestEvent>(async _ =>
        {
            firstStarted.TrySetResult();
            await release.Task;
        });
        eventBus.Subscribe<TestEvent>(async _ =>
        {
            secondStarted.TrySetResult();
            await release.Task;
        });

        var publication = eventBus.PublishAsync(new TestEvent()).AsTask();
        await Task.WhenAll(firstStarted.Task, secondStarted.Task).WaitAsync(TimeSpan.FromSeconds(5));

        release.TrySetResult();
        var result = await publication;

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.DeliveredCount);
        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task PublicationDeliveryFanOutHonorsConfiguredMaximumConcurrency()
    {
        var dispatchOptions = new EventBusDispatchOptions
        {
            MaximumConcurrentDeliveriesPerPublication = 3
        };
        await using var eventBus = new InMemoryEventBus(dispatchOptions: dispatchOptions);
        var firstBatchStarted = NewCompletion();
        var release = NewCompletion();
        var started = 0;
        var active = 0;
        var maximumActive = 0;

        for (var index = 0; index < 10; index++)
        {
            eventBus.Subscribe<TestEvent>(async _ =>
            {
                var currentActive = Interlocked.Increment(ref active);
                UpdateMaximum(ref maximumActive, currentActive);
                if (Interlocked.Increment(ref started) == 3)
                {
                    firstBatchStarted.TrySetResult();
                }

                await release.Task;
                Interlocked.Decrement(ref active);
            }, EventSubscriptionOptions.Current);
        }

        var publication = eventBus.PublishAsync(new TestEvent()).AsTask();
        await firstBatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, Volatile.Read(ref active));
        Assert.False(publication.IsCompleted);

        release.TrySetResult();
        var result = await publication.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.Succeeded);
        Assert.Equal(10, result.DeliveredCount);
        Assert.InRange(maximumActive, 1, 3);
    }

    [Fact]
    public async Task DisableSubscriptionQuiescesAfterConfiguredConsecutiveFailures()
    {
        await using var eventBus = new InMemoryEventBus();
        var invocationCount = 0;
        var subscription = eventBus.Subscribe<TestEvent>(
            _ =>
            {
                Interlocked.Increment(ref invocationCount);
                throw new InvalidOperationException("boom");
            },
            EventSubscriptionOptions.Serialized
                .WithErrorPolicy(EventErrorPolicy.DisableSubscription)
                .WithDisableSubscriptionAfterFailures(2));

        var first = await eventBus.PublishAsync(new TestEvent());
        var second = await eventBus.PublishAsync(new TestEvent());
        await subscription.StopAsync();
        var third = await eventBus.PublishAsync(new TestEvent());

        Assert.Equal(1, first.FailedCount);
        Assert.Equal(1, second.FailedCount);
        Assert.Empty(third.Deliveries);
        Assert.Equal(2, invocationCount);
        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
    }

    [Fact]
    public async Task DisableSubscriptionFailureCounterResetsAfterSuccess()
    {
        await using var eventBus = new InMemoryEventBus();
        var invocation = 0;
        var subscription = eventBus.Subscribe<TestEvent>(
            _ =>
            {
                var current = Interlocked.Increment(ref invocation);
                if (current is 1 or 3 or 4)
                {
                    throw new InvalidOperationException("boom");
                }

                return ValueTask.CompletedTask;
            },
            EventSubscriptionOptions.Serialized
                .WithErrorPolicy(EventErrorPolicy.DisableSubscription)
                .WithDisableSubscriptionAfterFailures(2));

        await eventBus.PublishAsync(new TestEvent());
        await eventBus.PublishAsync(new TestEvent());
        await eventBus.PublishAsync(new TestEvent());
        Assert.Equal(EventSubscriptionState.Active, subscription.State);

        await eventBus.PublishAsync(new TestEvent());
        await subscription.StopAsync();

        Assert.Equal(4, invocation);
        Assert.Equal(EventSubscriptionState.Disposed, subscription.State);
    }

    [Fact]
    public async Task StopPublicationCreatesSkippedResultsWithoutStartingLaterHandlers()
    {
        await using var eventBus = new InMemoryEventBus();
        var laterCalled = false;

        eventBus.Subscribe<TestEvent>(_ => ValueTask.CompletedTask);
        eventBus.Subscribe<TestEvent>(
            async _ =>
            {
                await Task.Yield();
                throw new InvalidOperationException("stop");
            },
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.StopPublication));
        eventBus.Subscribe<TestEvent>(_ =>
        {
            laterCalled = true;
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent());

        Assert.False(laterCalled);
        Assert.Equal(3, result.SubscriptionCount);
        Assert.Equal(2, result.DeliveredCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(EventDeliveryStatus.Skipped, result.Deliveries[2].Status);
    }

    [Fact]
    public async Task FailPublisherPropagatesAsynchronousHandlerFailure()
    {
        await using var eventBus = new InMemoryEventBus();
        eventBus.Subscribe<TestEvent>(
            async _ =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async-boom");
            },
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.FailPublisher));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await eventBus.PublishAsync(new TestEvent()));

        Assert.Equal("async-boom", exception.Message);
    }

    [Fact]
    public async Task CancellationWhileWaitingForSubscriptionSerialGateIsAStableDeliveryResult()
    {
        var channelOptions = new EventChannelOptions
        {
            ExecutionMode = EventChannelExecutionMode.Concurrent,
            MaximumConcurrency = 2,
            Capacity = 4
        };
        var diagnostics = new DeliveryStartDiagnostics(expectedCount: 2);
        await using var eventBus = new InMemoryEventBus(
            diagnostics: diagnostics,
            channelOptions: channelOptions);
        var firstStarted = NewCompletion();
        var releaseFirst = NewCompletion();

        eventBus.Subscribe<TestEvent>(async context =>
        {
            if (context.Event.Sequence == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
            }
        });

        var first = eventBus.PublishAsync(new TestEvent(1)).AsTask();
        await firstStarted.Task;
        using var cancellation = new CancellationTokenSource();
        var second = eventBus.PublishAsync(new TestEvent(2), cancellationToken: cancellation.Token).AsTask();
        try
        {
            await diagnostics.ExpectedDeliveriesStarted.WaitAsync(TimeSpan.FromSeconds(5));
            await cancellation.CancelAsync();

            var secondResult = await second.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(EventDeliveryStatus.Canceled, Assert.Single(secondResult.Deliveries).Status);
            Assert.Contains(
                diagnostics.Records,
                record => record.Code == EventDiagnosticIds.EventDeliveryCancelled);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await first;
        }
    }

    [Fact]
    public void OptionsRejectInvalidDispatchAndTimeoutBoundaries()
    {
        var dispatcher = new DeferredUiDispatcher();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventSubscriptionOptions.UiThread(dispatcher, (EventDispatchMode)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventSubscriptionOptions.Current.WithHandlerTimeout(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventSubscriptionOptions.Current.WithDisableSubscriptionAfterFailures(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InMemoryEventBus(dispatchOptions: new EventBusDispatchOptions
            {
                MaximumConcurrentDeliveriesPerPublication = 0
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InMemoryEventBus(dispatchOptions: new EventBusDispatchOptions
            {
                MaximumConcurrentDeliveriesPerPublication = 1025
            }));
    }

    [Fact]
    public void NewEnumsKeepStableValues()
    {
        Assert.Equal(3, (int)EventErrorPolicy.DisableSubscription);
        Assert.Equal(0, (int)EventDispatchMode.Post);
        Assert.Equal(1, (int)EventDispatchMode.InlineIfAllowed);
        Assert.Equal(0, (int)EventDeliveryStatus.Succeeded);
        Assert.Equal(4, (int)EventDeliveryStatus.Skipped);
    }

    [Fact]
    public void DeliveryResultRejectsContradictoryExtendedStatuses()
    {
        var successful = new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Current,
            Succeeded: true);

        Assert.Throws<ArgumentException>(() => successful with { TimedOut = true });
        Assert.Throws<ArgumentException>(() => successful with { Skipped = true });
        Assert.Throws<ArgumentException>(() => new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Current,
            Succeeded: false)
        {
            TimedOut = true,
            Skipped = true
        });
    }

    private static TaskCompletionSource NewCompletion()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref maximum);
            if (candidate <= current || Interlocked.CompareExchange(ref maximum, candidate, current) == current)
            {
                return;
            }
        }
    }

    private sealed record TestEvent(int Sequence = 0);

    private sealed class RecordingBackgroundScheduler : IEventBackgroundScheduler
    {
        public int RunCount { get; private set; }

        public async ValueTask RunAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            RunCount++;
            await callback(cancellationToken);
        }
    }

    private sealed class DeferredUiDispatcher : IUiDispatcher
    {
        private readonly Queue<(Func<CancellationToken, ValueTask> Callback, CancellationToken Token)> _queue = [];
        private readonly TaskCompletionSource _posted = NewCompletion();

        public Task Posted => _posted.Task;

        public bool CheckAccess() => false;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            callback();
            return ValueTask.CompletedTask;
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(callback());
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _queue.Enqueue((callback, cancellationToken));
            _posted.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public async ValueTask ExecuteNextAsync()
        {
            var work = _queue.Dequeue();
            await work.Callback(work.Token);
        }
    }

    private sealed class InlineUiDispatcher : IUiDispatcher
    {
        public int PostCount { get; private set; }

        public bool CheckAccess() => true;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            callback();
            return ValueTask.CompletedTask;
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(callback());
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            PostCount++;
            return callback(cancellationToken);
        }
    }

    private sealed class DeliveryStartDiagnostics : IHostDiagnostics
    {
        private readonly int _expectedCount;
        private readonly TaskCompletionSource _expectedDeliveriesStarted = NewCompletion();
        private readonly ConcurrentQueue<HostDiagnosticRecord> _records = new();
        private int _startedCount;

        public DeliveryStartDiagnostics(int expectedCount)
        {
            _expectedCount = expectedCount;
        }

        public Task ExpectedDeliveriesStarted => _expectedDeliveriesStarted.Task;

        public IReadOnlyList<HostDiagnosticRecord> Records => _records.ToArray();

        public void Write(HostDiagnosticRecord record)
        {
            _records.Enqueue(record);
            if (record.Code == EventDiagnosticIds.EventDeliveryStarted &&
                Interlocked.Increment(ref _startedCount) == _expectedCount)
            {
                _expectedDeliveriesStarted.TrySetResult();
            }
        }

        public void Complete()
        {
        }
    }
}
