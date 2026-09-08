namespace AtomUI.City.State;

public interface IStateScopeAccessor
{
    IStateScope? Current { get; }

    IDisposable Push(IStateScope scope);
}
