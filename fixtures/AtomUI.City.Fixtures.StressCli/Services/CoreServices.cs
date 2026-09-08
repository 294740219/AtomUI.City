namespace AtomUI.City.Fixtures.StressCli.Services;

// Foundation(4)

public interface IClockService
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClockService : IClockService
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface ISequenceService
{
    long Next();
}

public sealed class SequenceService : ISequenceService
{
    private long _current;
    public long Next() => Interlocked.Increment(ref _current);
}

public interface IEnvironmentProbe
{
    string MachineStamp { get; }
}

public sealed class EnvironmentProbeService : IEnvironmentProbe
{
    public string MachineStamp => $"{Environment.MachineName}/{Environment.ProcessId}";
}

public interface ICheckpointService
{
    long LastCheckpoint { get; set; }
}

public sealed class CheckpointService : ICheckpointService
{
    public long LastCheckpoint { get; set; }
}

// Telemetry(2)

public interface ITelemetrySink
{
    int Samples { get; }
    void Record(string sample);
}

public sealed class InMemoryTelemetrySink : ITelemetrySink
{
    private int _samples;
    public int Samples => _samples;
    public void Record(string sample) => Interlocked.Increment(ref _samples);
}

public interface IMetricsCollector
{
    int CountOf(string key);
    void Increment(string key);
}

public sealed class MetricsCollector : IMetricsCollector
{
    private readonly Dictionary<string, int> _counters = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public int CountOf(string key)
    {
        lock (_gate)
        {
            return _counters.TryGetValue(key, out var value) ? value : 0;
        }
    }

    public void Increment(string key)
    {
        lock (_gate)
        {
            _counters[key] = _counters.TryGetValue(key, out var value) ? value + 1 : 1;
        }
    }
}

// Settings(1)

public interface ISettingsStore
{
    string? Get(string key);
    void Set(string key, string value);
}

public sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;
    public void Set(string key, string value) => _values[key] = value;
}
