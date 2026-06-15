using System.Security.Claims;

namespace AtomUI.City.Security;

public sealed class AuthenticationStateSnapshot
{
    public AuthenticationStateSnapshot(
        AuthenticationState state,
        ClaimsPrincipal principal,
        long revision,
        string? scheme = null,
        DateTimeOffset? expiresAt = null,
        string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(principal);

        State = state;
        Principal = ClonePrincipal(principal);
        Revision = revision;
        Scheme = scheme;
        ExpiresAt = expiresAt;
        FailureMessage = failureMessage;
    }

    public AuthenticationState State { get; }

    public ClaimsPrincipal Principal { get; }

    public long Revision { get; }

    public string? Scheme { get; }

    public DateTimeOffset? ExpiresAt { get; }

    public string? FailureMessage { get; }

    private static ClaimsPrincipal ClonePrincipal(ClaimsPrincipal principal)
    {
        return new ClaimsPrincipal(principal.Identities.Select(CloneIdentity));
    }

    private static ClaimsIdentity CloneIdentity(ClaimsIdentity identity)
    {
        var clone = new ClaimsIdentity(
            identity.Claims.Select(CloneClaim),
            identity.AuthenticationType,
            identity.NameClaimType,
            identity.RoleClaimType)
        {
            Label = identity.Label,
            BootstrapContext = identity.BootstrapContext,
        };

        return clone;
    }

    private static Claim CloneClaim(Claim claim)
    {
        var clone = new Claim(
            claim.Type,
            claim.Value,
            claim.ValueType,
            claim.Issuer,
            claim.OriginalIssuer);

        foreach (var property in claim.Properties)
        {
            clone.Properties[property.Key] = property.Value;
        }

        return clone;
    }
}
