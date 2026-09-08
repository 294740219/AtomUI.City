namespace AtomUI.City.State;

internal static class ComputedStateEvaluationContext
{
    [ThreadStatic]
    private static HashSet<object>? _activeStates;

    public static IDisposable Enter(object state)
    {
        _activeStates ??= new HashSet<object>(ReferenceEqualityComparer.Instance);

        if (!_activeStates.Add(state))
        {
            throw new InvalidOperationException("A circular computed-state dependency was detected.");
        }

        return new ExitHandle(state);
    }

    private sealed class ExitHandle : IDisposable
    {
        private readonly object _state;
        private bool _disposed;

        public ExitHandle(object state)
        {
            _state = state;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _activeStates!.Remove(_state);
        }
    }
}
