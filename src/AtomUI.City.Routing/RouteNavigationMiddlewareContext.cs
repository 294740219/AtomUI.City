namespace AtomUI.City.Routing;

public sealed class RouteNavigationMiddlewareContext
{
    public RouteNavigationMiddlewareContext(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route,
        NavigationSnapshot currentSnapshot,
        IReadOnlyDictionary<string, string> parameters)
    {
        NavigationId = navigationId;
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Route = route ?? throw new ArgumentNullException(nameof(route));
        CurrentSnapshot = currentSnapshot ?? throw new ArgumentNullException(nameof(currentSnapshot));
        Parameters = RouteParameters.Copy(parameters ?? throw new ArgumentNullException(nameof(parameters)));
    }

    public Guid NavigationId { get; }
    public NavigationTarget Target { get; }
    public RouteDescriptor Route { get; }
    public NavigationSnapshot CurrentSnapshot { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }
}
