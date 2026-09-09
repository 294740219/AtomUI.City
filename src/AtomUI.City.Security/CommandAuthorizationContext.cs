namespace AtomUI.City.Security;

public sealed class CommandAuthorizationContext
{
    public CommandAuthorizationContext(
        string commandId,
        string? resourceName = null,
        string? contributionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);

        ValidateOptional(resourceName, nameof(resourceName));
        ValidateOptional(contributionId, nameof(contributionId));

        CommandId = commandId;
        ResourceName = resourceName;
        ContributionId = contributionId;
    }

    public string CommandId { get; }

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
