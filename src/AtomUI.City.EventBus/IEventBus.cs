using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.EventBus;

public interface IEventBus : IEventPublisher, IEventSubscriber, IDisposable
{
}

public interface IEventPublisher
{
    ValueTask<EventPublishResult> PublishAsync<TEvent>(
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<EventPostResult> PostAsync<TEvent>(
        TEvent eventData,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default);
}

public interface IEventSubscriber
{
    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        Func<EventContext<TEvent>, ValueTask> handler,
        EventSubscriptionOptions? options = null);

    IEventSubscription Subscribe<TEvent>(
        LifecycleScope owner,
        IEventHandler<TEvent> handler,
        EventSubscriptionOptions? options = null);
}
