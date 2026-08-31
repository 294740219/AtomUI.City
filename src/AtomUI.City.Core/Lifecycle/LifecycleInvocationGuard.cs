namespace AtomUI.City.Core.Lifecycle;

internal enum LifecycleOperationKind
{
    Start,
    Stop,
    Shutdown,
    Dispose,
}

internal static class LifecycleInvocationGuard
{
    private static readonly AsyncLocal<InvocationFrame?> CurrentFrame = new();

    [ThreadStatic]
    private static InvocationFrame? _currentSynchronousFrame;

    public static IDisposable Enter(object owner, LifecycleOperationKind operation)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var frame = new InvocationFrame(owner, operation, CurrentFrame.Value);
        CurrentFrame.Value = frame;

        return new InvocationScope(frame);
    }

    public static IDisposable EnterSynchronous(object owner, LifecycleOperationKind operation)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var frame = new InvocationFrame(owner, operation, _currentSynchronousFrame);
        _currentSynchronousFrame = frame;

        return new SynchronousInvocationScope(frame);
    }

    public static void ThrowIfReentrant(object owner, LifecycleOperationKind attemptedOperation)
    {
        ArgumentNullException.ThrowIfNull(owner);

        for (var frame = CurrentFrame.Value; frame is not null; frame = frame.Parent)
        {
            if (frame.IsActive && ReferenceEquals(frame.Owner, owner))
            {
                throw new InvalidOperationException(
                    $"Lifecycle operation '{attemptedOperation}' cannot be invoked recursively " +
                    $"while '{frame.Operation}' is executing for '{owner.GetType().Name}'.");
            }
        }

        for (var frame = _currentSynchronousFrame; frame is not null; frame = frame.Parent)
        {
            if (frame.IsActive && ReferenceEquals(frame.Owner, owner))
            {
                throw new InvalidOperationException(
                    $"Lifecycle operation '{attemptedOperation}' cannot be invoked recursively " +
                    $"while '{frame.Operation}' is executing for '{owner.GetType().Name}'.");
            }
        }
    }

    private sealed class InvocationFrame(
        object owner,
        LifecycleOperationKind operation,
        InvocationFrame? parent)
    {
        private int _active = 1;

        public object Owner { get; } = owner;

        public LifecycleOperationKind Operation { get; } = operation;

        public InvocationFrame? Parent { get; } = parent;

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public void Deactivate()
        {
            Interlocked.Exchange(ref _active, 0);
        }
    }

    private sealed class InvocationScope(InvocationFrame frame) : IDisposable
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
            CurrentFrame.Value = current.Parent;
        }
    }

    private sealed class SynchronousInvocationScope(InvocationFrame frame) : IDisposable
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
            _currentSynchronousFrame = current.Parent;
        }
    }
}
