namespace AtomUI.City.Routing;

public sealed class NavigationOptions
{
    public static NavigationOptions Default { get; } = new();

    public NavigationMode Mode { get; init; } = NavigationMode.Push;

    public NavigationHistoryBehavior HistoryBehavior { get; init; } = NavigationHistoryBehavior.Record;

    public NavigationConcurrencyPolicy ConcurrencyPolicy { get; init; } = NavigationConcurrencyPolicy.CancelPrevious;

    public string OutletName { get; init; } = "primary";

    internal bool RestoreState { get; init; }

    public bool ForceReload { get; init; }

    public bool AllowRedirect { get; init; } = true;

    public TimeSpan? Timeout { get; init; }

    public int JournalCapacity { get; init; } = 64;
}
