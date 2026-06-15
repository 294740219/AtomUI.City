using System.Security.Claims;
using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class AuthenticationStateTests
{
    [Fact]
    public void AuthenticationStateStoreStartsAsUnknownWithAnonymousPrincipal()
    {
        var store = new AuthenticationStateStore();

        Assert.Equal(AuthenticationState.Unknown, store.Current.State);
        Assert.Equal(0, store.Current.Revision);
        Assert.False(store.Current.Principal.Identity?.IsAuthenticated);
        Assert.Same(store.Current.Principal, ((ICurrentPrincipalAccessor)store).Principal);
    }

    [Fact]
    public void SetAuthenticatedPublishesSnapshotWithIncrementedRevision()
    {
        var store = new AuthenticationStateStore();
        var principal = CreatePrincipal("42", "settings.read");
        AuthenticationStateChangedEventArgs? observed = null;
        store.StateChanged += (_, args) => observed = args;

        var snapshot = store.SetAuthenticated(principal, scheme: "Bearer");

        Assert.Equal(AuthenticationState.Authenticated, snapshot.State);
        Assert.Equal(1, snapshot.Revision);
        Assert.Equal("Bearer", snapshot.Scheme);
        Assert.NotSame(principal, snapshot.Principal);
        Assert.Equal("42", snapshot.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Same(snapshot, store.Current);
        Assert.NotNull(observed);
        Assert.Equal(AuthenticationState.Unknown, observed.Previous.State);
        Assert.Same(snapshot, observed.Current);
    }

    [Fact]
    public void PublishedSnapshotsDoNotChangeWhenOriginalPrincipalIsMutated()
    {
        var store = new AuthenticationStateStore();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "42"),
                new Claim("permission", "settings.read"),
            ],
            authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        var snapshot = store.SetAuthenticated(principal, scheme: "Bearer");
        identity.AddClaim(new Claim("permission", "admin"));

        Assert.Equal(["settings.read"], snapshot.Principal.FindAll("permission").Select(claim => claim.Value));
        Assert.Equal(["settings.read"], store.Current.Principal.FindAll("permission").Select(claim => claim.Value));
    }

    [Fact]
    public void SetAnonymousIsIdempotentWhenStateAlreadyMatches()
    {
        var store = new AuthenticationStateStore();
        var eventCount = 0;
        store.StateChanged += (_, _) => eventCount++;

        var first = store.SetAnonymous();
        var second = store.SetAnonymous();

        Assert.Same(first, second);
        Assert.Equal(1, store.Current.Revision);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void SetSignedOutClearsPrincipalAndIncrementsRevision()
    {
        var store = new AuthenticationStateStore();
        store.SetAuthenticated(
            CreatePrincipal("42", "settings.read"),
            scheme: "Bearer",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30));

        var snapshot = store.SetSignedOut();

        Assert.Equal(AuthenticationState.SignedOut, snapshot.State);
        Assert.Equal(2, snapshot.Revision);
        Assert.False(snapshot.Principal.Identity?.IsAuthenticated);
        Assert.Null(snapshot.Scheme);
        Assert.Null(snapshot.ExpiresAt);
    }

    [Fact]
    public void SetFailedClearsPrincipalAndTokenHints()
    {
        var store = new AuthenticationStateStore();
        store.SetAuthenticated(
            CreatePrincipal("42", "settings.read"),
            scheme: "Bearer",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(30));

        var snapshot = store.SetFailed(
            "provider failed",
            CreatePrincipal("42", "settings.read"));

        Assert.Equal(AuthenticationState.Failed, snapshot.State);
        Assert.Equal("provider failed", snapshot.FailureMessage);
        Assert.False(snapshot.Principal.Identity?.IsAuthenticated);
        Assert.Null(snapshot.Scheme);
        Assert.Null(snapshot.ExpiresAt);
    }

    [Fact]
    public async Task DelegateAccessTokenProviderReturnsTokenForAuthenticatedPrincipal()
    {
        var provider = new DelegateAccessTokenProvider((context, cancellationToken) =>
        {
            Assert.Equal("api", context.ResourceName);
            Assert.False(cancellationToken.IsCancellationRequested);

            return ValueTask.FromResult(AccessTokenResult.Success("token-value", "Bearer"));
        });

        var result = await provider.GetTokenAsync(new AccessTokenRequest("api"));

        Assert.Equal(AccessTokenResultStatus.Success, result.Status);
        Assert.Equal("token-value", result.Token);
        Assert.Equal("Bearer", result.Scheme);
    }

    private static ClaimsPrincipal CreatePrincipal(string subject, string permission)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, subject),
                new Claim("permission", permission),
            ],
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }
}
