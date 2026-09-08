namespace AtomUI.City.Routing;

public delegate ValueTask<NavigationResult> RouteNavigationDelegate();

public interface IRouteNavigationMiddleware
{
    ValueTask<NavigationResult> InvokeAsync(
        RouteNavigationMiddlewareContext context,
        RouteNavigationDelegate next,
        CancellationToken cancellationToken);
}
