using System.Collections.Concurrent;

namespace AtomUI.City.Fixtures.StressCli.Infrastructure;

public sealed class StressOperationTrace(int capacity = 512)
{
    private readonly ConcurrentQueue<string> _entries = [];
    private readonly int _capacity = capacity > 0
        ? capacity
        : throw new ArgumentOutOfRangeException(nameof(capacity));
    private long _sequence;

    public void Record(int worker, string operation, string detail)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        _entries.Enqueue($"{sequence:D8} worker={worker:D2} operation={operation} {detail}");
        while (_entries.Count > _capacity && _entries.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<string> Snapshot() => _entries.ToArray();

    public void Print(TextWriter writer)
    {
        writer.WriteLine("=== 最后操作轨迹 ===");
        foreach (var entry in Snapshot())
        {
            writer.WriteLine(entry);
        }
    }
}
