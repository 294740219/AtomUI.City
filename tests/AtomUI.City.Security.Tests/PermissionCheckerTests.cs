using System.Security.Claims;
using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class PermissionCheckerTests
{
    [Fact]
    public async Task CheckAsyncAllowsPrincipalWithRegisteredPermissionClaim()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("settings.read"));
        var checker = new PermissionChecker(registry);
        var principal = CreatePrincipal(["settings.read"]);

        var result = await checker.CheckAsync(principal, "settings.read");

        Assert.Equal(AuthorizationResultStatus.Allowed, result.Status);
    }

    [Fact]
    public async Task CheckAsyncChallengesAnonymousPrincipal()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("settings.read"));
        var checker = new PermissionChecker(registry);

        var result = await checker.CheckAsync(new ClaimsPrincipal(new ClaimsIdentity()), "settings.read");

        Assert.Equal(AuthorizationResultStatus.Challenge, result.Status);
    }

    [Fact]
    public async Task CheckCurrentAsyncUsesCurrentPrincipalAccessor()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("settings.read"));
        var store = new AuthenticationStateStore();
        store.SetAuthenticated(CreatePrincipal(["settings.read"]));
        var checker = new PermissionChecker(registry, store);

        var result = await checker.CheckCurrentAsync("settings.read");

        Assert.Equal(AuthorizationResultStatus.Allowed, result.Status);
    }

    [Fact]
    public async Task CheckAsyncFailsForUnregisteredPermission()
    {
        var checker = new PermissionChecker(new PermissionRegistry());
        var principal = CreatePrincipal(["settings.read"]);

        var result = await checker.CheckAsync(principal, "settings.read");

        Assert.Equal(AuthorizationResultStatus.Failed, result.Status);
        Assert.Equal(SecurityFailureKind.PermissionNotFound, result.FailureKind);
        Assert.Equal("settings.read", result.FailedRequirement);
    }

    [Fact]
    public async Task CheckAsyncFailsAfterPermissionContributionIsRevoked()
    {
        var registry = new PermissionRegistry();
        registry.Add(new PermissionDescriptor("plugin.sales.export", contributionId: "SalesPlugin"));
        var checker = new PermissionChecker(registry);
        var principal = CreatePrincipal(["plugin.sales.export"]);

        registry.RemoveByContribution("SalesPlugin");
        var result = await checker.CheckAsync(principal, "plugin.sales.export");

        Assert.Equal(AuthorizationResultStatus.Failed, result.Status);
        Assert.Equal(SecurityFailureKind.PermissionNotFound, result.FailureKind);
    }

    [Fact]
    public async Task CheckAsyncMapsUnexpectedEvaluatorExceptionToFailedResult()
    {
        var exception = new OperationCanceledException("evaluator failed without caller cancellation");
        var checker = new PermissionChecker(new ThrowingAuthorizationEvaluator(exception));

        var result = await checker.CheckAsync(CreatePrincipal([]), "settings.read");

        Assert.Equal(AuthorizationResultStatus.Failed, result.Status);
        Assert.Equal(SecurityFailureKind.EvaluatorFailed, result.FailureKind);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task CheckAsyncRejectsNullEvaluatorResult()
    {
        var checker = new PermissionChecker(new NullAuthorizationEvaluator());

        var result = await checker.CheckAsync(CreatePrincipal([]), "settings.read");

        Assert.Equal(AuthorizationResultStatus.Failed, result.Status);
        Assert.Equal(SecurityFailureKind.EvaluatorFailed, result.FailureKind);
    }

    [Fact]
    public async Task CheckAsyncDoesNotAllowPermissionWhenEvaluatorCancelsBeforeReturning()
    {
        using var cancellation = new CancellationTokenSource();
        var checker = new PermissionChecker(new CancellingAuthorizationEvaluator(cancellation));

        var result = await checker.CheckAsync(
            CreatePrincipal(["settings.read"]),
            "settings.read",
            cancellation.Token);

        Assert.Equal(AuthorizationResultStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task CheckCurrentAsyncValidatesPermissionBeforeAccessorAvailability()
    {
        var checker = new PermissionChecker(new PermissionRegistry());

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await checker.CheckCurrentAsync(" "));
    }

    [Fact]
    public async Task CheckCurrentAsyncMapsPrincipalAccessorFailureToStableResult()
    {
        var exception = new InvalidOperationException("principal unavailable");
        var checker = new PermissionChecker(
            new AuthorizationEvaluator(new PermissionRegistry()),
            new ThrowingPrincipalAccessor(exception));

        var result = await checker.CheckCurrentAsync("settings.read");

        Assert.Equal(AuthorizationResultStatus.Failed, result.Status);
        Assert.Equal(SecurityFailureKind.EvaluatorFailed, result.FailureKind);
        Assert.Equal("settings.read", result.FailedRequirement);
        Assert.Same(exception, result.Exception);
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

    private sealed class ThrowingAuthorizationEvaluator(Exception exception) : IAuthorizationEvaluator
    {
        public ValueTask<AuthorizationResult> EvaluateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw exception;
        }

        public ValueTask<AuthorizationResult> EvaluatePolicyAsync(
            ClaimsPrincipal? principal,
            string policyName,
            string? resourceName = null,
            string? contributionId = null,
            CancellationToken cancellationToken = default)
        {
            throw exception;
        }
    }

    private sealed class NullAuthorizationEvaluator : IAuthorizationEvaluator
    {
        public ValueTask<AuthorizationResult> EvaluateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<AuthorizationResult>(null!);
        }

        public ValueTask<AuthorizationResult> EvaluatePolicyAsync(
            ClaimsPrincipal? principal,
            string policyName,
            string? resourceName = null,
            string? contributionId = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<AuthorizationResult>(null!);
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

    private sealed class ThrowingPrincipalAccessor(Exception exception) : ICurrentPrincipalAccessor
    {
        public ClaimsPrincipal Principal => throw exception;
    }
}
