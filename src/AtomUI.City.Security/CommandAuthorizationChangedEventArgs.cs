namespace AtomUI.City.Security;

public sealed class CommandAuthorizationChangedEventArgs : EventArgs
{
    public CommandAuthorizationChangedEventArgs(
        CommandAuthorizationChangeReason reason,
        long revision,
        string? commandId = null)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Change reason must be defined.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(revision);

        if (commandId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        }

        Reason = reason;
        Revision = revision;
        CommandId = commandId;
    }

    public CommandAuthorizationChangeReason Reason { get; }

    public long Revision { get; }

    public string? CommandId { get; }
}
