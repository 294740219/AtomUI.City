namespace AtomUI.City.Routing;

public interface IRouteRegistry : IRouteGraphProvider
{
    RouteContributionLease AddContribution(RouteContribution contribution);

    RouteContributionLease AddContribution(
        string contributionId,
        IReadOnlyList<RouteDescriptor> routes);

    bool RemoveContribution(string contributionId);
}
