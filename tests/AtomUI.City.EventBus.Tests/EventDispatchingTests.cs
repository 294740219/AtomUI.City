using AtomUI.City.EventBus;
using AtomUI.City.Core.Threading;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventDispatchingTests
{
    [Fact]
    public void EventDispatchPolicyKeepsStableValues()
    {
        Assert.Equal(0, (int)EventDispatchPolicy.Current);
        Assert.Equal(1, (int)EventDispatchPolicy.UiThread);
        Assert.Equal(2, (int)EventDispatchPolicy.Background);
        Assert.Equal(3, (int)EventDispatchPolicy.Serialized);
    }

    [Fact]
    public void EventErrorPolicyKeepsStableValues()
    {
        Assert.Equal(0, (int)EventErrorPolicy.ContinueAndReport);
        Assert.Equal(1, (int)EventErrorPolicy.StopPublication);
        Assert.Equal(2, (int)EventErrorPolicy.FailPublisher);
    }

    [Fact]
    public async Task DefaultSubscriptionUsesSerializedDispatchPolicy()
    {
        var eventBus = new InMemoryEventBus();

        eventBus.Subscribe<TestEvent>(_ => ValueTask.CompletedTask);

        var result = await eventBus.PublishAsync(new TestEvent("serialized"));
        var delivery = Assert.Single(result.Deliveries);

        Assert.Equal(EventDispatchPolicy.Serialized, delivery.DispatchPolicy);
    }

    [Fact]
    public async Task UiThreadSubscriptionUsesUiDispatcher()
    {
        var dispatcher = new RecordingDispatcher();
        var eventBus = new InMemoryEventBus();
        var received = string.Empty;

        eventBus.Subscribe<TestEvent>(
            context =>
            {
                received = context.Event.Value;
                return ValueTask.CompletedTask;
            },
            EventSubscriptionOptions.UiThread(dispatcher));

        var result = await eventBus.PublishAsync(new TestEvent("ui"));

        Assert.True(result.Succeeded);
        Assert.Equal("ui", received);
        Assert.Equal(1, dispatcher.PostCount);
        Assert.Equal(EventDispatchPolicy.UiThread, Assert.Single(result.Deliveries).DispatchPolicy);
    }

    [Fact]
    public async Task BackgroundSubscriptionRecordsBackgroundDispatchPolicy()
    {
        var eventBus = new InMemoryEventBus();
        var received = string.Empty;

        eventBus.Subscribe<TestEvent>(
            context =>
            {
                received = context.Event.Value;
                return ValueTask.CompletedTask;
            },
            EventSubscriptionOptions.Background());

        var result = await eventBus.PublishAsync(new TestEvent("background"));

        Assert.True(result.Succeeded);
        Assert.Equal("background", received);
        Assert.Equal(EventDispatchPolicy.Background, Assert.Single(result.Deliveries).DispatchPolicy);
    }

    [Fact]
    public async Task ContinueAndReportPolicyAggregatesFailureAndContinuesDelivery()
    {
        var eventBus = new InMemoryEventBus();
        var laterHandlerCalled = false;

        eventBus.Subscribe<TestEvent>(
            _ => throw new InvalidOperationException("boom"),
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.ContinueAndReport));
        eventBus.Subscribe<TestEvent>(_ =>
        {
            laterHandlerCalled = true;
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent("failure"));

        Assert.False(result.Succeeded);
        Assert.True(laterHandlerCalled);
        Assert.Equal(2, result.DeliveredCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1, result.Deliveries.Count(delivery => delivery.Succeeded));
    }

    [Fact]
    public async Task StopPublicationPolicySkipsLaterDeliveries()
    {
        var eventBus = new InMemoryEventBus();
        var laterHandlerCalled = false;

        eventBus.Subscribe<TestEvent>(
            _ => throw new InvalidOperationException("boom"),
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.StopPublication));
        eventBus.Subscribe<TestEvent>(_ =>
        {
            laterHandlerCalled = true;
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent("failure"));

        Assert.False(result.Succeeded);
        Assert.False(laterHandlerCalled);
        Assert.Equal(1, result.DeliveredCount);
        Assert.Equal(1, result.FailedCount);
    }

    [Fact]
    public async Task FailPublisherPolicyPropagatesHandlerFailure()
    {
        var eventBus = new InMemoryEventBus();

        eventBus.Subscribe<TestEvent>(
            _ => throw new InvalidOperationException("boom"),
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.FailPublisher));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await eventBus.PublishAsync(new TestEvent("failure")));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public void SubscriptionOptionsRejectUnknownErrorPolicy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventSubscriptionOptions.Serialized.WithErrorPolicy((EventErrorPolicy)999));
    }

    private sealed record TestEvent(string Value);

    private sealed class RecordingDispatcher : IUiDispatcher
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
}
