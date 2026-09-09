using System.Security.Claims;
using AtomUI.City.Security;
using AtomUI.City.Core.Diagnostics;

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
        Assert.NotSame(store.Current.Principal, ((ICurrentPrincipalAccessor)store).Principal);
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
                new Claim(SecurityClaimTypes.Permission, "settings.read"),
            ],
            authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        var snapshot = store.SetAuthenticated(principal, scheme: "Bearer");
        identity.AddClaim(new Claim(SecurityClaimTypes.Permission, "admin"));

        Assert.Equal(["settings.read"], snapshot.Principal.FindAll(SecurityClaimTypes.Permission).Select(claim => claim.Value));
        Assert.Equal(["settings.read"], store.Current.Principal.FindAll(SecurityClaimTypes.Permission).Select(claim => claim.Value));
    }

    [Fact]
    public void PublishedSnapshotCannotBeMutatedThroughPrincipalGetter()
    {
        var store = new AuthenticationStateStore();
        store.SetAuthenticated(CreatePrincipal("42", "settings.read"), scheme: "Bearer");

        var exposed = store.Current.Principal;
        ((ClaimsIdentity)exposed.Identity!).AddClaim(new Claim(SecurityClaimTypes.Permission, "settings.write"));

        Assert.DoesNotContain(
            store.Current.Principal.Claims,
            claim => claim.Type == SecurityClaimTypes.Permission && claim.Value == "settings.write");
        Assert.Equal(1, store.Current.Revision);
    }

    [Fact]
    public void PublishedPrincipalPreservesAndIsolatesTheActorChain()
    {
        var actor = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "delegating-user")],
            authenticationType: "Delegation")
        {
            BootstrapContext = "actor-secret",
        };
        var identity = new ClaimsIdentity(authenticationType: "Test")
        {
            Actor = actor,
        };
        var store = new AuthenticationStateStore();

        store.SetAuthenticated(new ClaimsPrincipal(identity));
        var firstActor = Assert.IsType<ClaimsIdentity>(
            Assert.IsType<ClaimsIdentity>(store.Current.Principal.Identity).Actor);
        firstActor.AddClaim(new Claim("actor-mutation", "value"));
        var secondActor = Assert.IsType<ClaimsIdentity>(
            Assert.IsType<ClaimsIdentity>(store.Current.Principal.Identity).Actor);

        Assert.Equal("delegating-user", secondActor.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Null(secondActor.BootstrapContext);
        Assert.False(secondActor.HasClaim("actor-mutation", "value"));
    }

    [Fact]
    public void ActorChangePublishesANewAuthenticationRevision()
    {
        var store = new AuthenticationStateStore();
        var first = CreatePrincipalWithActor("delegating-user-1");
        var second = CreatePrincipalWithActor("delegating-user-2");

        store.SetAuthenticated(first);
        var snapshot = store.SetAuthenticated(second);

        Assert.Equal(2, snapshot.Revision);
        var actor = Assert.IsType<ClaimsIdentity>(
            Assert.IsType<ClaimsIdentity>(snapshot.Principal.Identity).Actor);
        Assert.Equal("delegating-user-2", actor.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void PublishedSnapshotDoesNotRetainBootstrapCredential()
    {
        var identity = new ClaimsIdentity(authenticationType: "Test")
        {
            BootstrapContext = "raw-access-token",
        };
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "42"));
        var store = new AuthenticationStateStore();

        store.SetAuthenticated(new ClaimsPrincipal(identity), scheme: "Bearer");

        Assert.Null(((ClaimsIdentity)store.Current.Principal.Identity!).BootstrapContext);
    }

    [Fact]
    public async Task ConcurrentStateChangesPublishInRevisionOrder()
    {
        var store = new AuthenticationStateStore();
        using var firstEntered = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        var revisions = new List<long>();
        var revisionsSync = new object();
        store.StateChanged += (_, args) =>
        {
            if (args.Current.Revision == 1)
            {
                firstEntered.Set();
                releaseFirst.Wait(TimeSpan.FromSeconds(5));
            }

            lock (revisionsSync)
            {
                revisions.Add(args.Current.Revision);
            }
        };

        var first = Task.Run(store.SetAnonymous);
        Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)));
        var second = Task.Run(() => store.SetAuthenticating());
        await second;
        releaseFirst.Set();
        await first;

        Assert.Equal([1, 2], revisions);
    }

    [Fact]
    public void ObserverFailureDoesNotBreakStateCommitOrOtherObservers()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var store = new AuthenticationStateStore(diagnostics);
        var secondObserverCalled = false;
        store.StateChanged += (_, _) => throw new InvalidOperationException("observer failed");
        store.StateChanged += (_, _) => secondObserverCalled = true;

        var snapshot = store.SetAnonymous();

        Assert.Equal(AuthenticationState.Anonymous, snapshot.State);
        Assert.True(secondObserverCalled);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == SecurityDiagnosticIds.AuthenticationObserverFailed);
    }

    [Fact]
    public void SetAuthenticatedRejectsUnauthenticatedPrincipal()
    {
        var store = new AuthenticationStateStore();

        Assert.Throws<ArgumentException>(() =>
            store.SetAuthenticated(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void RefreshingAndExpiredStatesPreserveCurrentTokenHintsAtomically()
    {
        var store = new AuthenticationStateStore();
        var principal = CreatePrincipal("42", "settings.read");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);
        store.SetAuthenticated(principal, scheme: "Bearer", expiresAt);

        var refreshing = store.SetRefreshing(principal);
        var expired = store.SetExpired(principal);

        Assert.Equal(AuthenticationState.Refreshing, refreshing.State);
        Assert.Equal("Bearer", refreshing.Scheme);
        Assert.Equal(expiresAt, refreshing.ExpiresAt);
        Assert.Equal(AuthenticationState.Expired, expired.State);
        Assert.Equal("Bearer", expired.Scheme);
        Assert.Equal(expiresAt, expired.ExpiresAt);
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

        var snapshot = store.SetFailed("provider failed");

        Assert.Equal(AuthenticationState.Failed, snapshot.State);
        Assert.Equal("provider failed", snapshot.FailureMessage);
        Assert.False(snapshot.Principal.Identity?.IsAuthenticated);
        Assert.Null(snapshot.Scheme);
        Assert.Null(snapshot.ExpiresAt);
    }

    [Fact]
    public void SecurityPrincipalsAnonymousReturnsIndependentUnauthenticatedPrincipal()
    {
        var first = SecurityPrincipals.Anonymous;
        var second = SecurityPrincipals.Anonymous;

        ((ClaimsIdentity)first.Identity!).AddClaim(new Claim(SecurityClaimTypes.Permission, "mutated"));

        Assert.NotSame(first, second);
        Assert.False(first.Identity?.IsAuthenticated);
        Assert.False(second.Identity?.IsAuthenticated);
        Assert.DoesNotContain(second.Claims, claim => claim.Type == SecurityClaimTypes.Permission);
    }

    [Fact]
    public void CurrentPrincipalAccessorReturnsAuthenticatedSnapshotClaims()
    {
        var store = new AuthenticationStateStore();
        var accessor = (ICurrentPrincipalAccessor)store;

        store.SetAuthenticated(CreatePrincipal("42", "settings.read"), scheme: "Bearer");

        Assert.True(accessor.Principal.Identity?.IsAuthenticated);
        Assert.Equal("42", accessor.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Contains(
            accessor.Principal.Claims,
            claim => claim.Type == SecurityClaimTypes.Permission && claim.Value == "settings.read");
    }

    [Fact]
    public void PrincipalSnapshotReadRemainsStableAfterLaterStateChanges()
    {
        var store = new AuthenticationStateStore();
        var accessor = (ICurrentPrincipalAccessor)store;
        store.SetAuthenticated(CreatePrincipal("42", "settings.read"), scheme: "Bearer");
        var firstRead = accessor.Principal;

        store.SetAuthenticated(CreatePrincipal("84", "orders.read"), scheme: "Bearer");

        Assert.Equal("42", firstRead.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("84", accessor.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
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
                new Claim(SecurityClaimTypes.Permission, permission),
            ],
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreatePrincipalWithActor(string actorSubject)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test")
        {
            Actor = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actorSubject)],
                authenticationType: "Delegation"),
        });
    }
}
