using System.Security.Claims;

namespace AtomUI.City.Security;

public sealed class AuthorizationRequest
{
    private readonly ClaimsPrincipal _principal;

    public AuthorizationRequest(
        ClaimsPrincipal? principal,
        AuthorizationPolicy policy,
        string? resourceName = null,
        string? contributionId = null)
    {
        ArgumentNullException.ThrowIfNull(policy);

        ValidateOptional(resourceName, nameof(resourceName));
        ValidateOptional(contributionId, nameof(contributionId));

        _principal = SecurityPrincipalSnapshot.Clone(principal ?? SecurityPrincipals.Anonymous);
        Policy = policy;
        ResourceName = resourceName;
        ContributionId = contributionId;
    }

    public ClaimsPrincipal Principal => SecurityPrincipalSnapshot.Clone(_principal);

    internal ClaimsPrincipal PrincipalSnapshot => _principal;

    public AuthorizationPolicy Policy { get; }

    public string? ResourceName { get; }

    public string? ContributionId { get; }

    private static void ValidateOptional(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }
    }
}
