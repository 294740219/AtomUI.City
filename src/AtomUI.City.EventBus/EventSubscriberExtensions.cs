using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.EventBus;

public static class EventSubscriberExtensions
{
    public static IEventSubscription Subscribe<TEvent>(
        this IEventSubscriber subscriber,
        LifecycleScope owner,
        Action<EventContext<TEvent>> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);

        return subscriber.Subscribe<TEvent>(
            owner,
            context =>
            {
                handler(context);
                return ValueTask.CompletedTask;
            },
            options);
    }

    public static IEventSubscription Subscribe<TEvent>(
        this IEventSubscriber subscriber,
        LifecycleScope owner,
        EventChannel<TEvent> channel,
        Action<EventContext<TEvent>> handler,
        EventSubscriptionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(handler);
        EventChannel<TEvent>.ThrowIfDefault(channel, nameof(channel));

        return subscriber.Subscribe(
            owner,
            channel,
            context =>
            {
                handler(context);
                return ValueTask.CompletedTask;
            },
            options);
    }
}
