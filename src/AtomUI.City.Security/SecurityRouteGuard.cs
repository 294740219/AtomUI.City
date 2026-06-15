using AtomUI.City.Routing;

namespace AtomUI.City.Security;

public sealed class SecurityRouteGuard : IRouteEnterGuard
{
    private readonly IAuthorizationEvaluator _authorizationEvaluator;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly IRouteAuthorizationPolicyProvider _policyProvider;
    private readonly SecurityRouteGuardOptions _options;

    public SecurityRouteGuard(
        IAuthorizationEvaluator authorizationEvaluator,
        ICurrentPrincipalAccessor principalAccessor,
        IRouteAuthorizationPolicyProvider policyProvider)
        : this(
            authorizationEvaluator,
            principalAccessor,
            policyProvider,
            new SecurityRouteGuardOptions())
    {
    }

    public SecurityRouteGuard(
        IAuthorizationEvaluator authorizationEvaluator,
        ICurrentPrincipalAccessor principalAccessor,
        IRouteAuthorizationPolicyProvider policyProvider,
        SecurityRouteGuardOptions options)
    {
        ArgumentNullException.ThrowIfNull(authorizationEvaluator);
        ArgumentNullException.ThrowIfNull(principalAccessor);
        ArgumentNullException.ThrowIfNull(policyProvider);
        ArgumentNullException.ThrowIfNull(options);

        _authorizationEvaluator = authorizationEvaluator;
        _principalAccessor = principalAccessor;
        _policyProvider = policyProvider;
        _options = options;
    }

    public async ValueTask<RouteGuardResult> CanEnterAsync(
        RouteGuardContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (cancellationToken.IsCancellationRequested)
        {
            return RouteGuardResult.Cancel("Route authorization was cancelled.");
        }

        AuthorizationResult authorization;

        try
        {
            var policy = await _policyProvider.GetPolicyAsync(context, cancellationToken)
                .ConfigureAwait(false);

            if (policy is null)
            {
                return RouteGuardResult.Allow();
            }

            authorization = await _authorizationEvaluator.EvaluateAsync(
                    new AuthorizationRequest(
                        _principalAccessor.Principal,
                        policy,
                        resourceName: context.Route.RouteId),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return RouteGuardResult.Cancel("Route authorization was cancelled.");
        }
        catch (Exception exception)
        {
            return RouteGuardResult.Failed(
                SecurityRouteGuardResultCodes.AuthorizationFailed,
                exception.Message,
                exception);
        }

        return authorization.Status switch
        {
            AuthorizationResultStatus.Allowed => RouteGuardResult.Allow(),
            AuthorizationResultStatus.Challenge => CreateChallengeResult(authorization),
            AuthorizationResultStatus.Forbidden or AuthorizationResultStatus.Denied => RouteGuardResult.Reject(
                SecurityRouteGuardResultCodes.Forbidden,
                authorization.Message),
            AuthorizationResultStatus.Cancelled => RouteGuardResult.Cancel(authorization.Message),
            AuthorizationResultStatus.Failed => RouteGuardResult.Failed(
                SecurityRouteGuardResultCodes.AuthorizationFailed,
                authorization.Message ?? "Route authorization failed.",
                authorization.Exception),
            _ => RouteGuardResult.Failed(
                SecurityRouteGuardResultCodes.AuthorizationFailed,
                "Route authorization returned an unsupported result."),
        };
    }

    private RouteGuardResult CreateChallengeResult(AuthorizationResult authorization)
    {
        if (!string.IsNullOrWhiteSpace(_options.LoginRouteId))
        {
            return RouteGuardResult.Redirect(
                NavigationTarget.FromRouteReference(
                    _options.LoginRouteId,
                    parameters: null,
                    _options.LoginNavigationOptions));
        }

        return RouteGuardResult.Reject(
            SecurityRouteGuardResultCodes.AuthenticationRequired,
            authorization.Message);
    }
}
