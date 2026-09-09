namespace AtomUI.City.Security;

public sealed class AuthenticationStateChangedEventArgs : EventArgs
{
    public AuthenticationStateChangedEventArgs(
        AuthenticationStateSnapshot previous,
        AuthenticationStateSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        if (current.Revision <= previous.Revision)
        {
            throw new ArgumentException(
                "The current authentication snapshot revision must follow the previous revision.",
                nameof(current));
        }

        Previous = previous;
        Current = current;
    }

    public AuthenticationStateSnapshot Previous { get; }

    public AuthenticationStateSnapshot Current { get; }
}
