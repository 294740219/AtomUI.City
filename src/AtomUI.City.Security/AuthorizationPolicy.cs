namespace AtomUI.City.Security;

public sealed class AuthorizationPolicy
{
    public AuthorizationPolicy(
        string name,
        IReadOnlyCollection<AuthorizationRequirement> requirements,
        string? contributionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(requirements);

        var requirementSnapshot = requirements.ToArray();
        if (requirementSnapshot.Length == 0)
        {
            throw new ArgumentException("An authorization policy must contain at least one requirement.", nameof(requirements));
        }

        if (requirementSnapshot.Any(static requirement => requirement is null))
        {
            throw new ArgumentException("Authorization policy requirements cannot contain null values.", nameof(requirements));
        }

        if (contributionId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        }

        Name = name;
        Requirements = Array.AsReadOnly(requirementSnapshot);
        ContributionId = contributionId;
    }

    public string Name { get; }

    public IReadOnlyList<AuthorizationRequirement> Requirements { get; }

    public string? ContributionId { get; }

    public static AuthorizationPolicy RequireAuthenticated(string name)
    {
        return new AuthorizationPolicy(
            name,
            [AuthorizationRequirement.RequireAuthenticated()]);
    }

    public static AuthorizationPolicy RequirePermission(
        string name,
        string permissionName,
        string? contributionId = null)
    {
        return new AuthorizationPolicy(
            name,
            [
                AuthorizationRequirement.RequireAuthenticated(),
                AuthorizationRequirement.RequirePermission(permissionName),
            ],
            contributionId);
    }
}
