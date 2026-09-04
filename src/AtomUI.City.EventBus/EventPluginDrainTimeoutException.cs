namespace AtomUI.City.EventBus;

public sealed class EventPluginDrainTimeoutException : TimeoutException
{
    public EventPluginDrainTimeoutException(
        string pluginId,
        TimeSpan drainTimeout,
        int activeOperations,
        int activeSubscriptions,
        int pendingRegistrations)
        : base(CreateMessage(pluginId, drainTimeout, activeOperations, activeSubscriptions, pendingRegistrations))
    {
        PluginId = EventAttributeValidation.ValidateName(pluginId, nameof(pluginId));
        if (drainTimeout <= TimeSpan.Zero || drainTimeout > TimeSpan.FromMilliseconds(int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(nameof(drainTimeout), drainTimeout, "Drain timeout is outside the supported range.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(activeOperations);
        ArgumentOutOfRangeException.ThrowIfNegative(activeSubscriptions);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingRegistrations);
        DrainTimeout = drainTimeout;
        ActiveOperations = activeOperations;
        ActiveSubscriptions = activeSubscriptions;
        PendingRegistrations = pendingRegistrations;
    }

    public string PluginId { get; }

    public TimeSpan DrainTimeout { get; }

    public int ActiveOperations { get; }

    public int ActiveSubscriptions { get; }

    public int PendingRegistrations { get; }

    private static string CreateMessage(
        string pluginId,
        TimeSpan drainTimeout,
        int activeOperations,
        int activeSubscriptions,
        int pendingRegistrations)
    {
        return $"Plugin EventBus contribution '{pluginId}' did not drain within {drainTimeout}. " +
               $"Active operations: {activeOperations}; active subscriptions: {activeSubscriptions}; " +
               $"pending registrations: {pendingRegistrations}.";
    }
}
