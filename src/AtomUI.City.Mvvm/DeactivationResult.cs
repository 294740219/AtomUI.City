namespace AtomUI.City.Mvvm;

public sealed record DeactivationResult(
    DeactivationStatus Status,
    string? Reason = null,
    Exception? Exception = null)
{
    public static DeactivationResult Allow()
    {
        return new DeactivationResult(DeactivationStatus.Allow);
    }

    public static DeactivationResult Reject(string? reason = null)
    {
        return new DeactivationResult(DeactivationStatus.Reject, reason);
    }

    public static DeactivationResult Cancel(string? reason = null)
    {
        return new DeactivationResult(DeactivationStatus.Cancel, reason);
    }

    public static DeactivationResult Failed(string? reason = null, Exception? exception = null)
    {
        return new DeactivationResult(DeactivationStatus.Failed, reason, exception);
    }
}
