namespace AtomUI.City.State;

public interface IStateFactory
{
    WritableState<T> CreateWritable<T>(
        T initialValue,
        IEqualityComparer<T>? comparer = null,
        string? stateName = null,
        StateAccessPolicy access = StateAccessPolicy.HostWrite);

    ComputedState<T> CreateComputed<T>(
        Func<T> compute,
        params IReadOnlyState[] dependencies);

    StateScope CreateScope(string id);
}
