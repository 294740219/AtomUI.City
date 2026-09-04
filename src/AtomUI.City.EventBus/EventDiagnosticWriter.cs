using System.Diagnostics;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.EventBus;

internal sealed class EventDiagnosticWriter
{
    private readonly IHostDiagnostics? _sink;
    private readonly EventBusDiagnosticsOptions _options;
    private long _writeFailureCount;

    public EventDiagnosticWriter(IHostDiagnostics? sink, EventBusDiagnosticsOptions options)
    {
        _sink = sink;
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public long WriteFailureCount => Interlocked.Read(ref _writeFailureCount);

    public bool IsSampledTraceEnabled(Guid eventId)
    {
        return _sink is not null && ShouldSample(eventId);
    }

    public void Write(
        HostDiagnosticRecord record,
        bool sampledTrace = false,
        Guid eventId = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (_sink is null || sampledTrace && !ShouldSample(eventId))
        {
            return;
        }

        try
        {
            _sink.Write(record);
        }
        catch (Exception exception)
        {
            Interlocked.Increment(ref _writeFailureCount);
            try
            {
                Debug.WriteLine(
                    $"EventBus diagnostic sink rejected '{record.Code}': {exception.GetType().FullName}: {exception.Message}");
            }
            catch
            {
                // Diagnostics must never change the EventBus business result.
            }
        }
    }

    private bool ShouldSample(Guid eventId)
    {
        var rate = _options.TraceSamplingRate;
        if (rate >= 1d)
        {
            return true;
        }

        if (rate <= 0d || eventId == Guid.Empty)
        {
            return false;
        }

        var hash = unchecked((uint)eventId.GetHashCode());
        return hash / ((double)uint.MaxValue + 1d) < rate;
    }
}
