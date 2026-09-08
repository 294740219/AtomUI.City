using AtomUI.City.Fixtures.StressCli.Services;
using AtomUI.City.Routing;

namespace AtomUI.City.Fixtures.StressCli.Routing;

public sealed class FixtureRouteMatchPolicy(INavigationAudit audit) : IRouteMatchPolicy
{
    public ValueTask<bool> CanMatchAsync(RouteMatchPolicyContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        audit.Record($"policy:{context.Route.RouteId}");

        var canMatch = !string.Equals(
                           context.Route.RouteId,
                           "fixtures.routes.premium-search",
                           StringComparison.Ordinal) ||
                       context.Target.Path?.Contains("vip-", StringComparison.OrdinalIgnoreCase) == true;
        return ValueTask.FromResult(canMatch);
    }
}

public sealed class FixtureRouteGuard(INavigationAudit audit) : IRouteEnterGuard, IRouteLeaveGuard
{
    public ValueTask<RouteGuardResult> CanEnterAsync(RouteGuardContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        audit.Record($"guard-enter:{context.Route.RouteId}");
        return ValueTask.FromResult(
            context.Parameters.TryGetValue("id", out var id) && id == "13"
                ? RouteGuardResult.Reject("FIXTURE-ORDER-BLOCKED", "Order 13 is reserved for the rejection probe.")
                : RouteGuardResult.Allow());
    }

    public ValueTask<RouteGuardResult> CanLeaveAsync(RouteGuardContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        audit.Record($"guard-leave:{context.Route.RouteId}");
        return ValueTask.FromResult(RouteGuardResult.Allow());
    }
}

public sealed class FixtureRouteResolver(INavigationAudit audit) : IRouteResolver
{
    public ValueTask<RouteResolveResult> ResolveAsync(RouteResolveContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        audit.Record($"resolver:{context.Route.RouteId}");
        context.Parameters.TryGetValue("id", out var id);
        return ValueTask.FromResult(RouteResolveResult.Success(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["order"] = id is null ? null : $"resolved-order:{id}",
                ["navigationId"] = context.NavigationId,
            }));
    }
}

public sealed class FixtureRouteMiddleware(INavigationAudit audit) : IRouteNavigationMiddleware
{
    public async ValueTask<NavigationResult> InvokeAsync(
        RouteNavigationMiddlewareContext context,
        RouteNavigationDelegate next,
        CancellationToken cancellationToken)
    {
        audit.Record($"middleware-before:{context.Route.RouteId}");
        await Task.Delay(35, cancellationToken).ConfigureAwait(false);
        var result = await next().ConfigureAwait(false);
        audit.Record($"middleware-after:{context.Route.RouteId}:{result.Status}");
        return result;
    }
}
