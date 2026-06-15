using System.Security.Claims;

namespace AtomUI.City.Security;

public sealed class AuthenticationStateStore :
    IAuthenticationStateProvider,
    ICurrentPrincipalAccessor
{
    private readonly object _syncRoot = new();
    private AuthenticationStateSnapshot _current = new(
        AuthenticationState.Unknown,
        SecurityPrincipals.Anonymous,
        revision: 0);

    public event EventHandler<AuthenticationStateChangedEventArgs>? StateChanged;

    public AuthenticationStateSnapshot Current
    {
        get
        {
            lock (_syncRoot)
            {
                return _current;
            }
        }
    }

    public ClaimsPrincipal Principal => Current.Principal;

    public AuthenticationStateSnapshot SetAnonymous()
    {
        return SetCore(AuthenticationState.Anonymous, SecurityPrincipals.Anonymous);
    }

    public AuthenticationStateSnapshot SetAuthenticating(ClaimsPrincipal? principal = null, string? scheme = null)
    {
        return SetCore(AuthenticationState.Authenticating, principal ?? SecurityPrincipals.Anonymous, scheme);
    }

    public AuthenticationStateSnapshot SetAuthenticated(
        ClaimsPrincipal principal,
        string? scheme = null,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return SetCore(AuthenticationState.Authenticated, principal, scheme, expiresAt);
    }

    public AuthenticationStateSnapshot SetRefreshing(ClaimsPrincipal principal, string? scheme = null)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return SetCore(AuthenticationState.Refreshing, principal, scheme);
    }

    public AuthenticationStateSnapshot SetExpired(ClaimsPrincipal principal, string? scheme = null)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return SetCore(AuthenticationState.Expired, principal, scheme);
    }

    public AuthenticationStateSnapshot SetSignedOut()
    {
        return SetCore(AuthenticationState.SignedOut, SecurityPrincipals.Anonymous);
    }

    public AuthenticationStateSnapshot SetFailed(string failureMessage, ClaimsPrincipal? principal = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        return SetCore(
            AuthenticationState.Failed,
            SecurityPrincipals.Anonymous,
            scheme: null,
            expiresAt: null,
            failureMessage);
    }

    private AuthenticationStateSnapshot SetCore(
        AuthenticationState state,
        ClaimsPrincipal principal,
        string? scheme = null,
        DateTimeOffset? expiresAt = null,
        string? failureMessage = null)
    {
        AuthenticationStateSnapshot previous;
        AuthenticationStateSnapshot current;

        lock (_syncRoot)
        {
            previous = _current;
            if (Matches(previous, state, principal, scheme, expiresAt, failureMessage))
            {
                return previous;
            }

            current = new AuthenticationStateSnapshot(
                state,
                principal,
                previous.Revision + 1,
                scheme,
                expiresAt,
                failureMessage);
            _current = current;
        }

        StateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs(previous, current));

        return current;
    }

    private static bool Matches(
        AuthenticationStateSnapshot snapshot,
        AuthenticationState state,
        ClaimsPrincipal principal,
        string? scheme,
        DateTimeOffset? expiresAt,
        string? failureMessage)
    {
        return snapshot.State == state
            && string.Equals(snapshot.Scheme, scheme, StringComparison.Ordinal)
            && snapshot.ExpiresAt == expiresAt
            && string.Equals(snapshot.FailureMessage, failureMessage, StringComparison.Ordinal)
            && PrincipalsEqual(snapshot.Principal, principal);
    }

    private static bool PrincipalsEqual(ClaimsPrincipal left, ClaimsPrincipal right)
    {
        var leftIdentities = left.Identities.ToArray();
        var rightIdentities = right.Identities.ToArray();

        if (leftIdentities.Length != rightIdentities.Length)
        {
            return false;
        }

        for (var i = 0; i < leftIdentities.Length; i++)
        {
            if (!IdentitiesEqual(leftIdentities[i], rightIdentities[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IdentitiesEqual(ClaimsIdentity left, ClaimsIdentity right)
    {
        return string.Equals(left.AuthenticationType, right.AuthenticationType, StringComparison.Ordinal)
            && string.Equals(left.NameClaimType, right.NameClaimType, StringComparison.Ordinal)
            && string.Equals(left.RoleClaimType, right.RoleClaimType, StringComparison.Ordinal)
            && ClaimsEqual(left.Claims, right.Claims);
    }

    private static bool ClaimsEqual(IEnumerable<Claim> left, IEnumerable<Claim> right)
    {
        using var leftEnumerator = left.GetEnumerator();
        using var rightEnumerator = right.GetEnumerator();

        while (true)
        {
            var leftMoved = leftEnumerator.MoveNext();
            var rightMoved = rightEnumerator.MoveNext();

            if (leftMoved != rightMoved)
            {
                return false;
            }

            if (!leftMoved)
            {
                return true;
            }

            if (!ClaimsEqual(leftEnumerator.Current, rightEnumerator.Current))
            {
                return false;
            }
        }
    }

    private static bool ClaimsEqual(Claim left, Claim right)
    {
        return string.Equals(left.Type, right.Type, StringComparison.Ordinal)
            && string.Equals(left.Value, right.Value, StringComparison.Ordinal)
            && string.Equals(left.ValueType, right.ValueType, StringComparison.Ordinal)
            && string.Equals(left.Issuer, right.Issuer, StringComparison.Ordinal)
            && string.Equals(left.OriginalIssuer, right.OriginalIssuer, StringComparison.Ordinal);
    }
}
