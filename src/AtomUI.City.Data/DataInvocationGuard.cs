namespace AtomUI.City.Data;

internal static class DataInvocationGuard
{
    private static readonly AsyncLocal<InvocationFrame?> CurrentFrame = new();

    [ThreadStatic]
    private static InvocationFrame? _currentSynchronousFrame;

    public static void ThrowIfReentrant(object owner, string identity, string attemptedOperation)
    {
        ArgumentNullException.ThrowIfNull(owner);

        for (var frame = CurrentFrame.Value; frame is not null; frame = frame.Parent)
        {
            if (frame.IsActive && ReferenceEquals(frame.Owner, owner))
            {
                Throw(identity, attemptedOperation, frame.Operation);
            }
        }

        for (var frame = _currentSynchronousFrame; frame is not null; frame = frame.Parent)
        {
            if (frame.IsActive && ReferenceEquals(frame.Owner, owner))
            {
                Throw(identity, attemptedOperation, frame.Operation);
            }
        }
    }

    public static IDisposable Enter(object owner, string operation)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var frame = new InvocationFrame(owner, operation, CurrentFrame.Value);
        CurrentFrame.Value = frame;
        return new InvocationScope(frame, synchronous: false);
    }

    public static IDisposable EnterSynchronous(object owner, string operation)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var frame = new InvocationFrame(owner, operation, _currentSynchronousFrame);
        _currentSynchronousFrame = frame;
        return new InvocationScope(frame, synchronous: true);
    }

    private static void Throw(string identity, string attemptedOperation, string activeOperation)
    {
        throw new InvalidOperationException(
            $"Data connection '{identity}' cannot execute '{attemptedOperation}' recursively " +
            $"while '{activeOperation}' is running.");
    }

    private sealed class InvocationFrame(
        object owner,
        string operation,
        InvocationFrame? parent)
    {
        private int _active = 1;

        public object Owner { get; } = owner;

        public string Operation { get; } = operation;

        public InvocationFrame? Parent { get; } = parent;

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public void Deactivate() => Interlocked.Exchange(ref _active, 0);
    }

    private sealed class InvocationScope(InvocationFrame frame, bool synchronous) : IDisposable
    {
        private InvocationFrame? _frame = frame;

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _frame, null);
            if (current is null)
            {
                return;
            }

            current.Deactivate();
            if (synchronous)
            {
                _currentSynchronousFrame = current.Parent;
            }
            else
            {
                CurrentFrame.Value = current.Parent;
            }
        }
    }
}
