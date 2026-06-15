namespace AtomUI.City.Data;

public sealed class SignalRConnectionClosedException : Exception
{
    public SignalRConnectionClosedException(string? message = null)
        : base(message ?? "SignalR connection was closed.")
    {
    }

    public SignalRConnectionClosedException(string? message, Exception? innerException)
        : base(message ?? "SignalR connection was closed.", innerException)
    {
    }
}

public sealed class SignalRReconnectFailedException : Exception
{
    public SignalRReconnectFailedException(string? message = null)
        : base(message ?? "SignalR reconnect failed.")
    {
    }

    public SignalRReconnectFailedException(string? message, Exception? innerException)
        : base(message ?? "SignalR reconnect failed.", innerException)
    {
    }
}
