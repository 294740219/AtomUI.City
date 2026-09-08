namespace AtomUI.City.Routing;

internal interface IRouteContributionServiceResolver
{
    Func<Type, object?>? GetServiceResolver(RouteDescriptor route);
}
