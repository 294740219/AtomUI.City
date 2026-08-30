namespace AtomUI.City.Core.Hosting;

internal sealed class FreezableApplicationHostBuilderCollection<T>
{
    private readonly List<T> _items = [];
    private readonly object _syncRoot = new();
    private bool _frozen;

    public void Add(T item)
    {
        lock (_syncRoot)
        {
            ThrowIfFrozen();
            _items.Add(item);
        }
    }

    public void AddIfAbsent(T item, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        lock (_syncRoot)
        {
            ThrowIfFrozen();

            if (_items.Any(predicate))
            {
                return;
            }

            _items.Add(item);
        }
    }

    public IReadOnlyList<T> FreezeAndSnapshot()
    {
        lock (_syncRoot)
        {
            _frozen = true;
            return Array.AsReadOnly(_items.ToArray());
        }
    }

    private void ThrowIfFrozen()
    {
        if (_frozen)
        {
            throw new InvalidOperationException(
                "Application host builder is frozen after Build.");
        }
    }
}
