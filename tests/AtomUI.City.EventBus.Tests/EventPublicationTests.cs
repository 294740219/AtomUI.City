using AtomUI.City.Diagnostics;
using AtomUI.City.EventBus;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventPublicationTests
{
    [Fact]
    public async Task PublishAsyncInvokesMatchingHandlersWithEventContext()
    {
        var eventBus = new InMemoryEventBus();
        EventContext<TestEvent>? observedContext = null;

        eventBus.Subscribe<TestEvent>(context =>
        {
            observedContext = context;
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent("published"));

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.DeliveredCount);
        Assert.NotEqual(Guid.Empty, result.EventId);
        Assert.NotNull(observedContext);
        Assert.Equal("published", observedContext.Event.Value);
        Assert.Equal(result.EventId, observedContext.EventId);
        Assert.Equal(result.ContractId, observedContext.ContractId);
        Assert.Equal(0, observedContext.PublishDepth);
    }

    [Fact]
    public async Task PublishAsyncRejectsNullEvent()
    {
        var eventBus = new InMemoryEventBus();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await eventBus.PublishAsync<TestEvent>(null!));
    }

    [Fact]
    public async Task PublishAsyncObservesAlreadyCanceledTokenWithoutSubscriptions()
    {
        var eventBus = new InMemoryEventBus();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await eventBus.PublishAsync(
                new TestEvent("cancel"),
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task PublishAsyncRejectsNegativePublishDepth()
    {
        var eventBus = new InMemoryEventBus();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await eventBus.PublishAsync(
                new TestEvent("negative-depth"),
                new EventPublishOptions { PublishDepth = -1 }));
    }

    [Fact]
    public void PublishOptionsRejectNegativePublishDepthInit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventPublishOptions
        {
            PublishDepth = -1,
        });
    }

    [Fact]
    public async Task PostAsyncRejectsNullEvent()
    {
        var eventBus = new InMemoryEventBus();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await eventBus.PostAsync<TestEvent>(null!));
    }

    [Fact]
    public async Task PublishAsyncSupportsSyncAndAsyncHandlers()
    {
        var eventBus = new InMemoryEventBus();
        var received = new List<string>();

        eventBus.Subscribe<TestEvent>(context => received.Add("sync:" + context.Event.Value));
        eventBus.Subscribe<TestEvent>(async context =>
        {
            await Task.Yield();
            received.Add("async:" + context.Event.Value);
        });

        var result = await eventBus.PublishAsync(new TestEvent("value"));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.DeliveredCount);
        Assert.Contains("sync:value", received);
        Assert.Contains("async:value", received);
    }

    [Fact]
    public async Task PublishAsyncUsesRegisteredContractId()
    {
        var contracts = new InMemoryEventContractRegistry();
        var contractId = new EventContractId("atomui.city.tests.event.v1");
        contracts.Register(EventContractDescriptor.Shared<TestEvent>(contractId, typeof(TestEvent).Assembly));
        var eventBus = new InMemoryEventBus(contractRegistry: contracts);
        EventContractId observedContractId = default;

        eventBus.Subscribe<TestEvent>(context =>
        {
            observedContractId = context.ContractId;
            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PublishAsync(new TestEvent("published"));

        Assert.Equal(contractId, result.ContractId);
        Assert.Equal(contractId, observedContractId);
    }

    [Fact]
    public void PublishResultDeliveriesRejectExternalListMutation()
    {
        var delivery = new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: true);
        var replacement = new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Background,
            Succeeded: false,
            ErrorMessage: "replaced");
        var result = new EventPublishResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.event.v1"),
            [delivery]);
        var list = Assert.IsAssignableFrom<IList<EventDeliveryResult>>(result.Deliveries);

        Assert.Throws<NotSupportedException>(() => list[0] = replacement);
        Assert.Equal(delivery.SubscriptionId, result.Deliveries[0].SubscriptionId);
    }

    [Fact]
    public void PublishResultRejectsNullDeliveryEntries()
    {
        Assert.Throws<ArgumentException>(() => new EventPublishResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.event.v1"),
            [null!]));
    }

    [Fact]
    public void PublishResultRejectsEmptyEventId()
    {
        Assert.Throws<ArgumentException>(() => new EventPublishResult(
            Guid.Empty,
            new EventContractId("atomui.city.tests.event.v1"),
            []));
    }

    [Fact]
    public void PublishResultRejectsDefaultContractId()
    {
        Assert.Throws<ArgumentException>(() => new EventPublishResult(
            Guid.NewGuid(),
            default,
            []));
    }

    [Fact]
    public void DeliveryResultRejectsDefaultSubscriptionId()
    {
        Assert.Throws<ArgumentException>(() => new EventDeliveryResult(
            default,
            EventDispatchPolicy.Serialized,
            Succeeded: true));
    }

    [Fact]
    public void DeliveryResultRejectsUnknownDispatchPolicy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventDeliveryResult(
            EventSubscriptionId.New(),
            (EventDispatchPolicy)999,
            Succeeded: true));
    }

    [Fact]
    public void DeliveryResultRejectsSuccessfulCancellation()
    {
        Assert.Throws<ArgumentException>(() => new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: true,
            Canceled: true));
    }

    [Fact]
    public void DeliveryResultRejectsSuccessfulErrorMessage()
    {
        Assert.Throws<ArgumentException>(() => new EventDeliveryResult(
            EventSubscriptionId.New(),
            EventDispatchPolicy.Serialized,
            Succeeded: true,
            ErrorMessage: "should not be present"));
    }

    [Fact]
    public async Task PostAsyncReturnsAcceptedEventIdUsedByDelivery()
    {
        var eventBus = new InMemoryEventBus();
        var observedEventId = new TaskCompletionSource<Guid>();

        eventBus.Subscribe<TestEvent>(context =>
        {
            observedEventId.SetResult(context.EventId);

            return ValueTask.CompletedTask;
        });

        var result = await eventBus.PostAsync(new TestEvent("posted"));

        Assert.True(result.Accepted);
        Assert.Equal(result.EventId, await observedEventId.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task PostAsyncRejectsAlreadyCanceledPublication()
    {
        var eventBus = new InMemoryEventBus();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await eventBus.PostAsync(
            new TestEvent("posted"),
            cancellationToken: cancellation.Token);

        Assert.False(result.Accepted);
        Assert.NotEqual(Guid.Empty, result.EventId);
        Assert.False(string.IsNullOrWhiteSpace(result.RejectionReason));
    }

    [Fact]
    public void PostResultRejectsEmptyEventId()
    {
        Assert.Throws<ArgumentException>(() => new EventPostResult(
            Guid.Empty,
            new EventContractId("atomui.city.tests.event.v1"),
            Accepted: true));
    }

    [Fact]
    public void PostResultRejectsDefaultContractId()
    {
        Assert.Throws<ArgumentException>(() => new EventPostResult(
            Guid.NewGuid(),
            default,
            Accepted: true));
    }

    [Fact]
    public void PostResultEnforcesRejectionReasonConsistency()
    {
        Assert.Throws<ArgumentException>(() => new EventPostResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.event.v1"),
            Accepted: true,
            RejectionReason: "not rejected"));

        Assert.Throws<ArgumentException>(() => new EventPostResult(
            Guid.NewGuid(),
            new EventContractId("atomui.city.tests.event.v1"),
            Accepted: false));
    }

    [Fact]
    public async Task PostAsyncRejectsNegativePublishDepthBeforeAcceptance()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var eventBus = new InMemoryEventBus(diagnostics: diagnostics);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await eventBus.PostAsync(
                new TestEvent("negative-depth"),
                new EventPublishOptions { PublishDepth = -1 }));

        Assert.DoesNotContain(diagnostics.Records, record => record.Code == EventDiagnosticIds.EventAccepted);
    }

    private sealed record TestEvent(string Value);
}
