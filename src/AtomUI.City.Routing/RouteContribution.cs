namespace AtomUI.City.Routing;

public sealed class RouteContribution
{
    public RouteContribution(
        string contributionId,
        IReadOnlyList<RouteDescriptor> routes,
        Func<Type, object?>? serviceResolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ArgumentNullException.ThrowIfNull(routes);
        if (routes.Count == 0)
        {
            throw new ArgumentException("A route contribution must contain at least one route.", nameof(routes));
        }

        var ownedRoutes = new RouteDescriptor[routes.Count];
        for (var index = 0; index < routes.Count; index++)
        {
            var route = routes[index];
            if (route is null)
            {
                throw new RouteGraphException(
                    RouteGraphError.InvalidContribution,
                    $"Route contribution '{contributionId}' contains a null route descriptor.");
            }

            if (route.ContributionId is not null &&
                !string.Equals(route.ContributionId, contributionId, StringComparison.Ordinal))
            {
                throw new RouteGraphException(
                    RouteGraphError.InvalidContribution,
                    $"Route '{route.RouteId}' belongs to contribution '{route.ContributionId}', not '{contributionId}'.");
            }

            ownedRoutes[index] = route.ContributionId is null
                ? route.WithContributionId(contributionId)
                : route;
        }

        ContributionId = contributionId;
        Routes = Array.AsReadOnly(ownedRoutes);
        ServiceResolver = serviceResolver;
    }

    public string ContributionId { get; }
    public IReadOnlyList<RouteDescriptor> Routes { get; }
    public Func<Type, object?>? ServiceResolver { get; }
}
