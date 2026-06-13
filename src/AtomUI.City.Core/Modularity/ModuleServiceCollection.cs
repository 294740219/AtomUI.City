using System.Collections;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Modularity;

public sealed class ModuleServiceCollection : IServiceCollection
{
    private readonly IServiceCollection _inner;
    private bool _frozen;

    internal ModuleServiceCollection(IServiceCollection inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    public ServiceDescriptor this[int index]
    {
        get => _inner[index];
        set
        {
            ThrowIfFrozen();
            _inner[index] = value;
        }
    }

    public int Count => _inner.Count;

    public bool IsReadOnly => _frozen || _inner.IsReadOnly;

    internal void Freeze()
    {
        _frozen = true;
    }

    public void Add(ServiceDescriptor item)
    {
        ThrowIfFrozen();
        _inner.Add(item);
    }

    public void Clear()
    {
        ThrowIfFrozen();
        _inner.Clear();
    }

    public bool Contains(ServiceDescriptor item)
    {
        return _inner.Contains(item);
    }

    public void CopyTo(ServiceDescriptor[] array, int arrayIndex)
    {
        _inner.CopyTo(array, arrayIndex);
    }

    public IEnumerator<ServiceDescriptor> GetEnumerator()
    {
        return _inner.GetEnumerator();
    }

    public int IndexOf(ServiceDescriptor item)
    {
        return _inner.IndexOf(item);
    }

    public void Insert(int index, ServiceDescriptor item)
    {
        ThrowIfFrozen();
        _inner.Insert(index, item);
    }

    public bool Remove(ServiceDescriptor item)
    {
        ThrowIfFrozen();

        return _inner.Remove(item);
    }

    public void RemoveAt(int index)
    {
        ThrowIfFrozen();
        _inner.RemoveAt(index);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private void ThrowIfFrozen()
    {
        if (_frozen)
        {
            throw new InvalidOperationException(
                "Module service collection is frozen after the service configuration phase.");
        }
    }
}
