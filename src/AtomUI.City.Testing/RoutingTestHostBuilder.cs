namespace AtomUI.City.Testing;

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
        ThrowIfRouteConflicts();
        _built = true;

        return new RoutingTestHost(_routes.ToArray());
    }

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("The routing test host builder has already built a host and is frozen.");
        }
    }

    private void ThrowIfRouteConflicts()
    {
        var duplicateName = _routes
            .GroupBy(route => route.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateName is not null)
        {
            throw new InvalidOperationException($"Duplicate route name '{duplicateName.Key}'.");
        }

        var duplicatePattern = _routes
            .GroupBy(route => NormalizePattern(route.Pattern), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatePattern is not null)
        {
            throw new InvalidOperationException($"Duplicate route pattern '{duplicatePattern.Key}'.");
        }
    }

    private static string NormalizePattern(string pattern)
    {
        var segments = pattern
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => segment.StartsWith('{') && segment.EndsWith('}') ? "{}" : segment.ToLowerInvariant());

        return "/" + string.Join("/", segments);
    }
}
