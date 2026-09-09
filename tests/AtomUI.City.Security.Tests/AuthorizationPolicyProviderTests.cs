using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class AuthorizationPolicyProviderTests
{
    [Fact]
    public void AddStoresPolicyByName()
    {
        var provider = new InMemoryAuthorizationPolicyProvider();
        var policy = AuthorizationPolicy.RequireAuthenticated("SignedIn");

        var added = provider.Add(policy);

        Assert.True(added);
        Assert.Equal(1, provider.Revision);
        Assert.True(provider.TryGet("SignedIn", out var stored));
        Assert.Same(policy, stored);
    }

    [Fact]
    public void AddRejectsDuplicatePolicyWithoutChangingRevision()
    {
        var provider = new InMemoryAuthorizationPolicyProvider();
        provider.Add(AuthorizationPolicy.RequireAuthenticated("SignedIn"));

        var added = provider.Add(AuthorizationPolicy.RequireAuthenticated("SignedIn"));

        Assert.False(added);
        Assert.Equal(1, provider.Revision);
    }

    [Fact]
    public void RemoveByContributionRevokesMatchingPolicies()
    {
        var provider = new InMemoryAuthorizationPolicyProvider();
        provider.Add(AuthorizationPolicy.RequireAuthenticated("HostPolicy"));
        provider.Add(AuthorizationPolicy.RequirePermission("PluginPolicy", "plugin.sales.export", contributionId: "SalesPlugin"));

        var removed = provider.RemoveByContribution("SalesPlugin");

        Assert.Equal(1, removed);
        Assert.Equal(3, provider.Revision);
        Assert.True(provider.Contains("HostPolicy"));
        Assert.False(provider.Contains("PluginPolicy"));
    }

    [Fact]
    public void PoliciesRejectsExternalListMutation()
    {
        var provider = new InMemoryAuthorizationPolicyProvider();
        provider.Add(AuthorizationPolicy.RequireAuthenticated("SignedIn"));

        var policies = provider.Policies;

        var mutable = Assert.IsAssignableFrom<IList<AuthorizationPolicy>>(policies);
        Assert.Throws<NotSupportedException>(() => mutable[0] = AuthorizationPolicy.RequireAuthenticated("Other"));
    }

    [Fact]
    public void RevokedContributionCannotRegisterAnotherPolicy()
    {
        var provider = new InMemoryAuthorizationPolicyProvider();
        provider.Add(AuthorizationPolicy.RequirePermission(
            "PluginPolicy",
            "plugin.sales.export",
            contributionId: "SalesPlugin"));

        provider.RemoveByContribution("SalesPlugin");
        var added = provider.Add(AuthorizationPolicy.RequirePermission(
            "ReplacementPolicy",
            "plugin.sales.import",
            contributionId: "SalesPlugin"));

        Assert.False(added);
        Assert.False(provider.Contains("ReplacementPolicy"));
    }

    [Fact]
    public async Task CancelledLookupThrowsInsteadOfMasqueradingAsMissingPolicy()
    {
        var provider = new InMemoryAuthorizationPolicyProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await provider.GetPolicyAsync("SignedIn", cancellation.Token));
    }
}
