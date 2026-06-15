using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class AuthorizationEvaluatorTests
{
    [Fact]
    public async Task AuthenticatedPolicyChallengesAnonymousPrincipal()
    {
        var evaluator = CreateEvaluator();
        var request = new AuthorizationRequest(
            ClaimsPrincipal.Current,
            AuthorizationPolicy.RequireAuthenticated("SignedIn"));

        var result = await evaluator.EvaluateAsync(request);

        Assert.Equal(AuthorizationResultStatus.Challenge, result.Status);
        Assert.Equal(SecurityFailureKind.AuthenticationRequired, result.FailureKind);
        Assert.Equal("Errors.AuthenticationRequired", result.MessageKey);
    }

    [Fact]
    public async Task PermissionRequirementAllowsMatchingPermissionClaim()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("settings.read"));
        var evaluator = CreateEvaluator(registry);
        var principal = CreatePrincipal(
            permissions: ["settings.read"],
            claims: [],
            roles: []);
        var request = new AuthorizationRequest(
            principal,
            AuthorizationPolicy.RequirePermission("CanReadSettings", "settings.read"));

        var result = await evaluator.EvaluateAsync(request);

        Assert.Equal(AuthorizationResultStatus.Allowed, result.Status);
    }

    [Fact]
    public async Task PermissionRequirementForAuthenticatedPrincipalWithoutPermissionReturnsForbidden()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("settings.write"));
        var evaluator = CreateEvaluator(registry);
        var request = new AuthorizationRequest(
            CreatePrincipal(permissions: ["settings.read"], claims: [], roles: []),
            AuthorizationPolicy.RequirePermission("CanWriteSettings", "settings.write"));

        var result = await evaluator.EvaluateAsync(request);

        Assert.Equal(AuthorizationResultStatus.Forbidden, result.Status);
        Assert.Equal(SecurityFailureKind.RequirementFailed, result.FailureKind);
        Assert.Equal("settings.write", result.FailedRequirement);
        Assert.Equal("Errors.AuthorizationForbidden", result.MessageKey);
        Assert.Equal(["settings.write"], result.MessageArguments);
    }

    [Fact]
    public async Task UnknownPermissionReturnsFailedResult()
    {
        var evaluator = CreateEvaluator();
        var request = new AuthorizationRequest(
            CreatePrincipal(permissions: ["settings.read"], claims: [], roles: []),
            AuthorizationPolicy.RequirePermission("CanReadSettings", "settings.read"));

        var result = await evaluator.EvaluateAsync(request);

        Assert.Equal(AuthorizationResultStatus.Failed, result.Status);
        Assert.Equal(SecurityFailureKind.PermissionNotFound, result.FailureKind);
    }

    [Fact]
    public async Task ClaimAndRoleRequirementsAreEvaluated()
    {
        var evaluator = CreateEvaluator();
        var principal = CreatePrincipal(
            permissions: [],
            claims: [new Claim("department", "finance")],
            roles: ["admin"]);
        var policy = new AuthorizationPolicy(
            "CanAdminFinance",
            [
                AuthorizationRequirement.RequireClaim("department", "finance"),
                AuthorizationRequirement.RequireRole("admin"),
            ]);

        var result = await evaluator.EvaluateAsync(new AuthorizationRequest(principal, policy));

        Assert.Equal(AuthorizationResultStatus.Allowed, result.Status);
    }

    [Fact]
    public async Task CancelledEvaluationReturnsCancelledResult()
    {
        var evaluator = CreateEvaluator();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await evaluator.EvaluateAsync(
            new AuthorizationRequest(ClaimsPrincipal.Current, AuthorizationPolicy.RequireAuthenticated("SignedIn")),
            cancellation.Token);

        Assert.Equal(AuthorizationResultStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task EvaluatePolicyAsyncAllowsPolicyFromProvider()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("settings.read"));
        var provider = new StubAuthorizationPolicyProvider(
            (name, _) => name == "CanReadSettings"
                ? new ValueTask<AuthorizationPolicy?>(AuthorizationPolicy.RequirePermission(name, "settings.read"))
                : new ValueTask<AuthorizationPolicy?>((AuthorizationPolicy?)null));
        var evaluator = CreateEvaluator(registry, provider);
        var principal = CreatePrincipal(
            permissions: ["settings.read"],
            claims: [],
            roles: []);

        var result = await evaluator.EvaluatePolicyAsync(principal, "CanReadSettings");

        Assert.Equal(AuthorizationResultStatus.Allowed, result.Status);
    }

    [Fact]
    public async Task EvaluatePolicyAsyncReturnsPolicyNotFoundWhenProviderReturnsNull()
    {
        var evaluator = CreateEvaluator(
            policyProvider: new StubAuthorizationPolicyProvider(
                (_, _) => new ValueTask<AuthorizationPolicy?>((AuthorizationPolicy?)null)));

        var result = await evaluator.EvaluatePolicyAsync(
            CreatePrincipal(permissions: [], claims: [], roles: []),
            "MissingPolicy");

        Assert.Equal(AuthorizationResultStatus.Failed, result.Status);
        Assert.Equal(SecurityFailureKind.PolicyNotFound, result.FailureKind);
        Assert.Equal("MissingPolicy", result.FailedRequirement);
    }

    [Fact]
    public async Task EvaluatePolicyAsyncMapsProviderExceptionToFailedResult()
    {
        var exception = new InvalidOperationException("Policy catalog failed.");
        var evaluator = CreateEvaluator(
            policyProvider: new StubAuthorizationPolicyProvider((_, _) => throw exception));

        var result = await evaluator.EvaluatePolicyAsync(
            CreatePrincipal(permissions: [], claims: [], roles: []),
            "BrokenPolicy");

        Assert.Equal(AuthorizationResultStatus.Failed, result.Status);
        Assert.Equal(SecurityFailureKind.EvaluatorFailed, result.FailureKind);
        Assert.Equal("BrokenPolicy", result.FailedRequirement);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task EvaluatePolicyAsyncReturnsCancelledWithoutCallingProvider()
    {
        var providerCalled = false;
        var evaluator = CreateEvaluator(
            policyProvider: new StubAuthorizationPolicyProvider(
                (_, _) =>
                {
                    providerCalled = true;
                    return new ValueTask<AuthorizationPolicy?>(
                        AuthorizationPolicy.RequireAuthenticated("SignedIn"));
                }));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await evaluator.EvaluatePolicyAsync(
            CreatePrincipal(permissions: [], claims: [], roles: []),
            "SignedIn",
            cancellationToken: cancellation.Token);

        Assert.Equal(AuthorizationResultStatus.Cancelled, result.Status);
        Assert.False(providerCalled);
    }

    private static AuthorizationEvaluator CreateEvaluator(
        PermissionRegistry? registry = null,
        IAuthorizationPolicyProvider? policyProvider = null)
    {
        return policyProvider is null
            ? new AuthorizationEvaluator(registry ?? new PermissionRegistry())
            : new AuthorizationEvaluator(registry ?? new PermissionRegistry(), policyProvider);
    }

    private static ClaimsPrincipal CreatePrincipal(
        IReadOnlyCollection<string> permissions,
        IReadOnlyCollection<Claim> claims,
        IReadOnlyCollection<string> roles)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "42"));

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim("permission", permission));
        }

        foreach (var claim in claims)
        {
            identity.AddClaim(claim);
        }

        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(identity);
    }

    private sealed class StubAuthorizationPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly Func<string, CancellationToken, ValueTask<AuthorizationPolicy?>> _getPolicy;

        public StubAuthorizationPolicyProvider(
            Func<string, CancellationToken, ValueTask<AuthorizationPolicy?>> getPolicy)
        {
            _getPolicy = getPolicy;
        }

        public long Revision => 0;

        public IReadOnlyCollection<AuthorizationPolicy> Policies => [];

        public bool Contains(string name)
        {
            return false;
        }

        public bool TryGet(
            string name,
            [NotNullWhen(true)]
            out AuthorizationPolicy? policy)
        {
            policy = null;
            return false;
        }

        public ValueTask<AuthorizationPolicy?> GetPolicyAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            return _getPolicy(name, cancellationToken);
        }
    }
}
