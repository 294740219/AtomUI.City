namespace AtomUI.City.Mvvm;

public sealed class CommandExecutionState
{
    private readonly object _gate = new();

    public CommandExecutionState(
        string? commandName = null,
        Type? ownerType = null)
    {
        CommandName = commandName;
        OwnerType = ownerType;
    }

    public string? CommandName { get; }

    public Type? OwnerType { get; }

    public bool IsExecuting { get; private set; }

    public OperationResult? LastResult { get; private set; }

    public OperationResult? LastRejectedResult { get; private set; }

    public int RejectedExecutionCount { get; private set; }

    public Exception? LastError { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    internal bool TryBegin(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (IsExecuting)
            {
                return false;
            }

            IsExecuting = true;
            LastResult = null;
            LastError = null;
            CancellationToken = cancellationToken;

            return true;
        }
    }

    internal void Complete(OperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            LastResult = result;
            LastError = result.Error;
            IsExecuting = false;
        }
    }

    internal void Reject(OperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            LastRejectedResult = result;
            RejectedExecutionCount++;
        }
    }
}
