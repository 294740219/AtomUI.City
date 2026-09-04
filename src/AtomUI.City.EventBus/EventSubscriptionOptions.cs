using AtomUI.City.Core.Threading;

namespace AtomUI.City.EventBus;

public sealed class EventSubscriptionOptions
{
    private EventSubscriptionOptions(
        EventDispatchPolicy dispatchPolicy,
        EventDispatchMode dispatchMode,
        IUiDispatcher? uiDispatcher,
        EventErrorPolicy errorPolicy,
        TimeSpan? handlerTimeout,
        int disableSubscriptionAfterFailures)
    {
        DispatchPolicy = dispatchPolicy;
        DispatchMode = dispatchMode;
        UiDispatcher = uiDispatcher;
        ErrorPolicy = errorPolicy;
        HandlerTimeout = ValidateHandlerTimeout(handlerTimeout);
        DisableSubscriptionAfterFailures = ValidateDisableThreshold(disableSubscriptionAfterFailures);
    }

    public static EventSubscriptionOptions Serialized { get; } = new(
        EventDispatchPolicy.Serialized,
        EventDispatchMode.InlineIfAllowed,
        uiDispatcher: null,
        EventErrorPolicy.ContinueAndReport,
        handlerTimeout: TimeSpan.FromSeconds(30),
        disableSubscriptionAfterFailures: 3);

    public static EventSubscriptionOptions Current { get; } = new(
        EventDispatchPolicy.Current,
        EventDispatchMode.InlineIfAllowed,
        uiDispatcher: null,
        EventErrorPolicy.ContinueAndReport,
        handlerTimeout: TimeSpan.FromSeconds(30),
        disableSubscriptionAfterFailures: 3);

    public EventDispatchPolicy DispatchPolicy { get; }

    public EventDispatchMode DispatchMode { get; }

    public IUiDispatcher? UiDispatcher { get; }

    public EventErrorPolicy ErrorPolicy { get; }

    public TimeSpan? HandlerTimeout { get; }

    public int DisableSubscriptionAfterFailures { get; }

    public static EventSubscriptionOptions UiThread(
        IUiDispatcher dispatcher,
        EventDispatchMode dispatchMode = EventDispatchMode.Post)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ValidateDispatchMode(dispatchMode);

        return new EventSubscriptionOptions(
            EventDispatchPolicy.UiThread,
            dispatchMode,
            dispatcher,
            EventErrorPolicy.ContinueAndReport,
            handlerTimeout: TimeSpan.FromSeconds(30),
            disableSubscriptionAfterFailures: 3);
    }

    public static EventSubscriptionOptions Background()
    {
        return new EventSubscriptionOptions(
            EventDispatchPolicy.Background,
            EventDispatchMode.Post,
            uiDispatcher: null,
            EventErrorPolicy.ContinueAndReport,
            handlerTimeout: TimeSpan.FromSeconds(30),
            disableSubscriptionAfterFailures: 3);
    }

    public EventSubscriptionOptions WithErrorPolicy(EventErrorPolicy errorPolicy)
    {
        if (!Enum.IsDefined(errorPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(errorPolicy),
                errorPolicy,
                "Event error policy is not supported.");
        }

        return new EventSubscriptionOptions(
            DispatchPolicy,
            DispatchMode,
            UiDispatcher,
            errorPolicy,
            HandlerTimeout,
            DisableSubscriptionAfterFailures);
    }

    public EventSubscriptionOptions WithHandlerTimeout(TimeSpan? handlerTimeout)
    {
        return new EventSubscriptionOptions(
            DispatchPolicy,
            DispatchMode,
            UiDispatcher,
            ErrorPolicy,
            handlerTimeout,
            DisableSubscriptionAfterFailures);
    }

    public EventSubscriptionOptions WithDisableSubscriptionAfterFailures(int failureCount)
    {
        return new EventSubscriptionOptions(
            DispatchPolicy,
            DispatchMode,
            UiDispatcher,
            ErrorPolicy,
            HandlerTimeout,
            failureCount);
    }

    private static TimeSpan? ValidateHandlerTimeout(TimeSpan? handlerTimeout)
    {
        if (handlerTimeout is { } timeout &&
            (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(
                nameof(handlerTimeout),
                handlerTimeout,
                $"Handler timeout must be greater than zero and no greater than {int.MaxValue} milliseconds.");
        }

        return handlerTimeout;
    }

    private static int ValidateDisableThreshold(int failureCount)
    {
        if (failureCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureCount),
                failureCount,
                "Disable-subscription failure threshold must be greater than zero.");
        }

        return failureCount;
    }

    private static void ValidateDispatchMode(EventDispatchMode dispatchMode)
    {
        if (!Enum.IsDefined(dispatchMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dispatchMode),
                dispatchMode,
                "Event dispatch mode is not supported.");
        }
    }
}
