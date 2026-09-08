namespace AtomUI.City.Testing;

using AtomUI.City.Routing;

public sealed class RoutingTestHostBuilder
{
    private readonly List<RouteTestDefinition> _routes = [];
    private bool _built;

    public RoutingTestHostBuilder MapRoute(string name, string pattern, Type viewModelType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(viewModelType);
        ThrowIfBuilt();

        _routes.Add(new RouteTestDefinition(name, pattern, viewModelType));

        return this;
    }

    public RoutingTestHost Build()
    {
        ThrowIfBuilt();
        _built = true;

        try
        {
            return new RoutingTestHost(_routes.ToArray());
        }
        catch (RouteGraphException exception)
        {
            throw new InvalidOperationException("The routing test graph is invalid.", exception);
        }
    }

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("The routing test host builder has already built a host and is frozen.");
        }
    }

}
