namespace AtomUI.City.Routing;

public interface IRouteResolver
{
    ValueTask<RouteResolveResult> ResolveAsync(
        RouteResolveContext context,
        CancellationToken cancellationToken);
}
