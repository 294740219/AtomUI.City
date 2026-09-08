namespace AtomUI.City.State;

public sealed class StateScopeAccessor : IStateScopeAccessor
{
    private readonly AsyncLocal<IStateScope?> _current = new();

    public IStateScope? Current => _current.Value;

    public IDisposable Push(IStateScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var previous = _current.Value;
        _current.Value = scope;

        return new RestoreHandle(this, previous);
    }

    private sealed class RestoreHandle : IDisposable
    {
        private readonly StateScopeAccessor _accessor;
        private readonly IStateScope? _previous;
        private int _disposed;

        public RestoreHandle(StateScopeAccessor accessor, IStateScope? previous)
        {
            _accessor = accessor;
            _previous = previous;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _accessor._current.Value = _previous;
        }
    }
}
