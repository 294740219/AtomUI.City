namespace AtomUI.City.Routing;

public sealed class NavigationSnapshot
{
    private NavigationSnapshot(
        RouteDescriptor? activeRoute,
        IReadOnlyDictionary<string, string> parameters,
        long routeGraphVersion,
        string? reuseKey,
        IReadOnlyDictionary<string, object?>? resolvedData)
    {
        ActiveRoute = activeRoute;
        Parameters = RouteParameters.Copy(parameters);
        RouteGraphVersion = routeGraphVersion;
        ReuseKey = string.IsNullOrWhiteSpace(reuseKey) ? null : reuseKey;
        ResolvedData = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(resolvedData ?? new Dictionary<string, object?>(), StringComparer.Ordinal));
    }

    public RouteDescriptor Route => ActiveRoute ?? throw new InvalidOperationException("Navigation snapshot does not have an active route.");

    public RouteDescriptor? ActiveRoute { get; }

    public IReadOnlyDictionary<string, string> Parameters { get; }

    public long RouteGraphVersion { get; }

    public string? ReuseKey { get; }

    public IReadOnlyDictionary<string, object?> ResolvedData { get; }

    public static NavigationSnapshot Empty(long routeGraphVersion)
    {
        return new NavigationSnapshot(
            activeRoute: null,
            RouteParameters.Empty(),
            routeGraphVersion,
            reuseKey: null,
            resolvedData: null);
    }

    public static NavigationSnapshot FromRoute(
        RouteDescriptor activeRoute,
        IReadOnlyDictionary<string, string> parameters,
        long routeGraphVersion,
        string? reuseKey = null,
        IReadOnlyDictionary<string, object?>? resolvedData = null)
    {
        ArgumentNullException.ThrowIfNull(activeRoute);
        ArgumentNullException.ThrowIfNull(parameters);

        return new NavigationSnapshot(
            activeRoute,
            parameters,
            routeGraphVersion,
            reuseKey,
            resolvedData);
    }
}
