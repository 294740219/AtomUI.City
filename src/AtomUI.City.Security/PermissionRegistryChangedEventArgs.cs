namespace AtomUI.City.Security;

public sealed class PermissionRegistryChangedEventArgs : EventArgs
{
    public PermissionRegistryChangedEventArgs(
        long revision,
        string? permissionName = null,
        string? contributionId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(revision);

        if (permissionName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        }

        if (contributionId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        }

        Revision = revision;
        PermissionName = permissionName;
        ContributionId = contributionId;
    }

    public long Revision { get; }

    public string? PermissionName { get; }

    public string? ContributionId { get; }
}
