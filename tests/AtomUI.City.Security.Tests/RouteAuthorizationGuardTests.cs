using System.Security.Claims;
using AtomUI.City.Routing;
using AtomUI.City.Security;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security.Tests;

public sealed class RouteAuthorizationGuardTests
{
    [Fact]
    public async Task GuardAllowsRouteWithoutAuthorizationPolicy()
    {
        var guard = CreateGuard();
        var context = CreateContext("home");

        var result = await guard.CanEnterAsync(context, CancellationToken.None);

        Assert.Equal(RouteGuardResultStatus.Allow, result.Status);
    }

    [Fact]
    public async Task GuardRejectsProtectedRouteForAnonymousPrincipal()
    {
        var guard = CreateGuard(policyProvider =>
        {
            policyProvider.Add("settings", AuthorizationPolicy.RequireAuthenticated("SignedIn"));
        });
        var context = CreateContext("settings");

        var result = await guard.CanEnterAsync(context, CancellationToken.None);

        Assert.Equal(RouteGuardResultStatus.Reject, result.Status);
        Assert.Equal(SecurityRouteGuardResultCodes.AuthenticationRequired, result.Code);
    }

    [Fact]
    public async Task GuardRedirectsChallengeToConfiguredLoginRoute()
    {
        var guard = CreateGuard(
            policyProvider =>
            {
                policyProvider.Add("settings", AuthorizationPolicy.RequireAuthenticated("SignedIn"));
            },
            options: new SecurityRouteGuardOptions
            {
                LoginRouteId = "login",
            });
        var context = CreateContext("settings");

        var result = await guard.CanEnterAsync(context, CancellationToken.None);

        Assert.Equal(RouteGuardResultStatus.Redirect, result.Status);
        Assert.NotNull(result.RedirectTarget);
        Assert.Equal("login", result.RedirectTarget.RouteId);
    }

    [Fact]
    public async Task GuardRejectsForbiddenRouteForAuthenticatedPrincipalWithoutPermission()
    {
        var store = new AuthenticationStateStore();
        store.SetAuthenticated(CreatePrincipal(permissions: []));
        var permissions = new PermissionRegistry();
        permissions.Add(new PermissionDescriptor("settings.read"));
        var guard = CreateGuard(
            policyProvider =>
            {
                policyProvider.Add("settings", AuthorizationPolicy.RequirePermission("CanReadSettings", "settings.read"));
            },
            store,
            permissions);
        var context = CreateContext("settings");

        var result = await guard.CanEnterAsync(context, CancellationToken.None);

        Assert.Equal(RouteGuardResultStatus.Reject, result.Status);
        Assert.Equal(SecurityRouteGuardResultCodes.Forbidden, result.Code);
    }

    [Fact]
    public async Task GuardAllowsAuthorizedRoute()
    {
        var store = new AuthenticationStateStore();
        store.SetAuthenticated(CreatePrincipal(permissions: ["settings.read"]));
        var permissions = new PermissionRegistry();
        permissions.Add(new PermissionDescriptor("settings.read"));
        var guard = CreateGuard(
            policyProvider =>
            {
                policyProvider.Add("settings", AuthorizationPolicy.RequirePermission("CanReadSettings", "settings.read"));
            },
            store,
            permissions);
        var context = CreateContext("settings");

        var result = await guard.CanEnterAsync(context, CancellationToken.None);

        Assert.Equal(RouteGuardResultStatus.Allow, result.Status);
    }

    [Fact]
    public async Task GuardCancellationDoesNotAllowNavigation()
    {
        var guard = CreateGuard(policyProvider =>
        {
            policyProvider.Add("settings", AuthorizationPolicy.RequireAuthenticated("SignedIn"));
        });
        var context = CreateContext("settings");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await guard.CanEnterAsync(context, cancellation.Token);

        Assert.Equal(RouteGuardResultStatus.Cancel, result.Status);
    }

    [Fact]
    public async Task GuardDoesNotAllowNavigationWhenProviderCancelsBeforeReturning()
    {
        using var cancellation = new CancellationTokenSource();
        var guard = new SecurityRouteGuard(
            new AuthorizationEvaluator(new PermissionRegistry()),
            new AuthenticationStateStore(),
            new CancellingRouteAuthorizationPolicyProvider(cancellation));

        var result = await guard.CanEnterAsync(CreateContext("settings"), cancellation.Token);

        Assert.Equal(RouteGuardResultStatus.Cancel, result.Status);
    }

    [Fact]
    public async Task GuardDoesNotAllowNavigationWhenEvaluatorCancelsBeforeReturning()
    {
        using var cancellation = new CancellationTokenSource();
        var policies = new InMemoryRouteAuthorizationPolicyProvider();
        policies.Add("settings", AuthorizationPolicy.RequireAuthenticated("SignedIn"));
        var guard = new SecurityRouteGuard(
            new CancellingAuthorizationEvaluator(cancellation),
            new AuthenticationStateStore(),
            policies);

        var result = await guard.CanEnterAsync(CreateContext("settings"), cancellation.Token);

        Assert.Equal(RouteGuardResultStatus.Cancel, result.Status);
    }

    [Fact]
    public async Task GuardMapsPolicyProviderExceptionToFailedResult()
    {
        var exception = new InvalidOperationException("Policy provider failed.");
        var guard = new SecurityRouteGuard(
            new AuthorizationEvaluator(new PermissionRegistry()),
            new AuthenticationStateStore(),
            new ThrowingRouteAuthorizationPolicyProvider(exception));
        var context = CreateContext("settings");

        var result = await guard.CanEnterAsync(context, CancellationToken.None);

        Assert.Equal(RouteGuardResultStatus.Failed, result.Status);
        Assert.Equal(SecurityRouteGuardResultCodes.AuthorizationFailed, result.Code);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task GuardMapsUnexpectedOperationCancelledExceptionToFailedResult()
    {
        var exception = new OperationCanceledException("provider failed without caller cancellation");
        var guard = new SecurityRouteGuard(
            new AuthorizationEvaluator(new PermissionRegistry()),
            new AuthenticationStateStore(),
            new ThrowingRouteAuthorizationPolicyProvider(exception));

        var result = await guard.CanEnterAsync(CreateContext("settings"), CancellationToken.None);

        Assert.Equal(RouteGuardResultStatus.Failed, result.Status);
        Assert.Equal(SecurityRouteGuardResultCodes.AuthorizationFailed, result.Code);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public void RoutePolicyContributionRevocationRemovesPoliciesAndRejectsReregistration()
    {
        var provider = new InMemoryRouteAuthorizationPolicyProvider();
        provider.Add(
            "sales",
            AuthorizationPolicy.RequirePermission(
                "SalesPolicy",
                "plugin.sales.read",
                contributionId: "SalesPlugin"));

        var removed = provider.RemoveByContribution("SalesPlugin");
        var added = provider.Add(
            "sales-new",
            AuthorizationPolicy.RequirePermission(
                "SalesPolicy2",
                "plugin.sales.write",
                contributionId: "SalesPlugin"));

        Assert.Equal(1, removed);
        Assert.False(added);
    }

    [Fact]
    public async Task RouteAuthorizationWritesRoutePolicyAndResultDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var provider = new InMemoryRouteAuthorizationPolicyProvider();
        provider.Add("settings", AuthorizationPolicy.RequireAuthenticated("SignedIn"));
        var guard = new SecurityRouteGuard(
            new AuthorizationEvaluator(new PermissionRegistry()),
            new AuthenticationStateStore(),
            provider,
            new SecurityRouteGuardOptions(),
            diagnostics);

        var result = await guard.CanEnterAsync(CreateContext("settings"), CancellationToken.None);

        Assert.Equal(RouteGuardResultStatus.Reject, result.Status);
        var record = Assert.Single(
            diagnostics.Records,
            item => item.Code == SecurityDiagnosticIds.RouteAuthorizationCompleted);
        Assert.Equal("settings", record.Context["routeId"]);
        Assert.Equal("SignedIn", record.Context["policyName"]);
        Assert.Equal("Reject", record.Context["resultStatus"]);
    }

    [Fact]
    public void RoutingAssemblyDoesNotReferenceSecurity()
    {
        var routingReferences = typeof(RouteGuardContext).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            routingReferences,
            reference => string.Equals(reference.Name, "AtomUI.City.Security", StringComparison.Ordinal));
    }

    private static SecurityRouteGuard CreateGuard(
        Action<InMemoryRouteAuthorizationPolicyProvider>? configurePolicyProvider = null,
        AuthenticationStateStore? store = null,
        PermissionRegistry? permissions = null,
        SecurityRouteGuardOptions? options = null)
    {
        var policyProvider = new InMemoryRouteAuthorizationPolicyProvider();
        configurePolicyProvider?.Invoke(policyProvider);
        store ??= new AuthenticationStateStore();
        permissions ??= new PermissionRegistry();

        return options is null
            ? new SecurityRouteGuard(
                new AuthorizationEvaluator(permissions),
                store,
                policyProvider)
            : new SecurityRouteGuard(
                new AuthorizationEvaluator(permissions),
                store,
                policyProvider,
                options);
    }

    private static RouteGuardContext CreateContext(string routeId)
    {
        var route = new RouteDescriptor(
            routeId,
            RouteDefinitionKind.Route,
            routeId,
            new ViewModelTargetDescriptor(typeof(TestViewModel)));
        var target = NavigationTarget.FromRouteReference(routeId, parameters: null, NavigationOptions.Default);

        return new RouteGuardContext(
            Guid.NewGuid(),
            target,
            route,
            NavigationSnapshot.Empty(0),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static ClaimsPrincipal CreatePrincipal(IReadOnlyCollection<string> permissions)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim(SecurityClaimTypes.Permission, permission));
        }

        return new ClaimsPrincipal(identity);
    }

    private sealed class TestViewModel;

    private sealed class ThrowingRouteAuthorizationPolicyProvider : IRouteAuthorizationPolicyProvider
    {
        private readonly Exception _exception;

        public ThrowingRouteAuthorizationPolicyProvider(Exception exception)
        {
            _exception = exception;
        }

        public ValueTask<AuthorizationPolicy?> GetPolicyAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class CancellingRouteAuthorizationPolicyProvider(
        CancellationTokenSource cancellation) : IRouteAuthorizationPolicyProvider
    {
        public ValueTask<AuthorizationPolicy?> GetPolicyAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromResult<AuthorizationPolicy?>(null);
        }
    }

    private sealed class CancellingAuthorizationEvaluator(
        CancellationTokenSource cancellation) : IAuthorizationEvaluator
    {
        public ValueTask<AuthorizationResult> EvaluateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromResult(AuthorizationResult.Allowed());
        }

        public ValueTask<AuthorizationResult> EvaluatePolicyAsync(
            ClaimsPrincipal? principal,
            string policyName,
            string? resourceName = null,
            string? contributionId = null,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromResult(AuthorizationResult.Allowed());
        }
    }
}
