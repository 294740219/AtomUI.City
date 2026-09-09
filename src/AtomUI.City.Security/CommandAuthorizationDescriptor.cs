namespace AtomUI.City.Security;

public sealed class CommandAuthorizationDescriptor
{
    public CommandAuthorizationDescriptor(
        string commandId,
        AuthorizationPolicy policy,
        CommandUnauthorizedBehavior unauthorizedBehavior = CommandUnauthorizedBehavior.Disable,
        string? deniedMessageKey = null,
        string? contributionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(policy);

        if (!Enum.IsDefined(unauthorizedBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(unauthorizedBehavior),
                unauthorizedBehavior,
                "Command unauthorized behavior must be defined.");
        }

        if (contributionId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        }

        if (contributionId is not null
            && policy.ContributionId is not null
            && !string.Equals(contributionId, policy.ContributionId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A command descriptor and its policy cannot belong to different contributions.",
                nameof(contributionId));
        }

        if (deniedMessageKey is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(deniedMessageKey);
        }

        CommandId = commandId;
        Policy = policy;
        UnauthorizedBehavior = unauthorizedBehavior;
        DeniedMessageKey = deniedMessageKey;
        ContributionId = contributionId ?? policy.ContributionId;
    }

    public string CommandId { get; }

    public AuthorizationPolicy Policy { get; }

    public CommandUnauthorizedBehavior UnauthorizedBehavior { get; }

    public string? DeniedMessageKey { get; }

    public string? ContributionId { get; }
}
