using System.Collections;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents module service collection.
/// </summary>
public sealed class ModuleServiceCollection : IServiceCollection
{
    private readonly IServiceCollection _inner;
    private bool _frozen;

    internal ModuleServiceCollection(IServiceCollection inner)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
    }

    /// <summary>
    /// Gets the this[int] value.
    /// </summary>
    public ServiceDescriptor this[int index]
    {
        get => _inner[index];
        set
        {
            ThrowIfFrozen();
            _inner[index] = value;
        }
    }

    /// <summary>
    /// Gets the count value.
    /// </summary>
    public int Count => _inner.Count;

    /// <summary>
    /// Gets the is read only value.
    /// </summary>
    public bool IsReadOnly => _frozen || _inner.IsReadOnly;

    internal void Freeze()
    {
        _frozen = true;
    }

    /// <summary>
    /// Executes the add operation.
    /// </summary>
    public void Add(ServiceDescriptor item)
    {
        ThrowIfFrozen();
        _inner.Add(item);
    }

    /// <summary>
    /// Executes the clear operation.
    /// </summary>
    public void Clear()
    {
        ThrowIfFrozen();
        _inner.Clear();
    }

    /// <summary>
    /// Executes the contains operation.
    /// </summary>
    public bool Contains(ServiceDescriptor item)
    {
        return _inner.Contains(item);
    }

    /// <summary>
    /// Executes the copy to operation.
    /// </summary>
    public void CopyTo(ServiceDescriptor[] array, int arrayIndex)
    {
        _inner.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Executes the get enumerator operation.
    /// </summary>
    public IEnumerator<ServiceDescriptor> GetEnumerator()
    {
        return _inner.GetEnumerator();
    }

    /// <summary>
    /// Executes the index of operation.
    /// </summary>
    public int IndexOf(ServiceDescriptor item)
    {
        return _inner.IndexOf(item);
    }

    /// <summary>
    /// Executes the insert operation.
    /// </summary>
    public void Insert(int index, ServiceDescriptor item)
    {
        ThrowIfFrozen();
        _inner.Insert(index, item);
    }

    /// <summary>
    /// Executes the remove operation.
    /// </summary>
    public bool Remove(ServiceDescriptor item)
    {
        ThrowIfFrozen();

        return _inner.Remove(item);
    }

    /// <summary>
    /// Executes the remove at operation.
    /// </summary>
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
