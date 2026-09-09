using System.Security.Claims;

namespace AtomUI.City.Security;

internal static class SecurityPrincipalSnapshot
{
    public static ClaimsPrincipal Clone(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

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
        };

        if (identity.Actor is not null)
        {
            clone.Actor = CloneIdentity(identity.Actor);
        }

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
