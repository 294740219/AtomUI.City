using System.Collections.Concurrent;
using AtomUI.City.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.CoreEventBus.DogfoodApp.Verification;

public interface IDogfoodLedger
{
    void Record(string handler, string businessId);
    int Total { get; }
    IReadOnlyDictionary<string, int> Snapshot();
    void Reset();
}

[Service(ServiceLifetime.Singleton), ExposeServices(typeof(IDogfoodLedger))]
public sealed class DogfoodLedger : IDogfoodLedger
{
    private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.Ordinal);
    private int _total;

    public int Total => Volatile.Read(ref _total);

    public void Record(string handler, string businessId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(businessId);
        _counts.AddOrUpdate(handler, 1, static (_, count) => count + 1);
        Interlocked.Increment(ref _total);
    }

    public IReadOnlyDictionary<string, int> Snapshot() => new Dictionary<string, int>(_counts);

    public void Reset()
    {
        _counts.Clear();
        Interlocked.Exchange(ref _total, 0);
    }
}
