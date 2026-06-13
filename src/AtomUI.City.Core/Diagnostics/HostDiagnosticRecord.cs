using System.Collections.ObjectModel;
using AtomUI.City.Lifecycle;

namespace AtomUI.City.Diagnostics;

public sealed record HostDiagnosticRecord(
    string Code,
    string Message,
    HostDiagnosticSeverity Severity,
    string? ScopeId = null,
    LifecycleStage? Stage = null)
{
    private IReadOnlyDictionary<string, string?> _context =
        new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>(StringComparer.Ordinal));

    public IReadOnlyDictionary<string, string?> Context
    {
        get => _context;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            _context = new ReadOnlyDictionary<string, string?>(
                new Dictionary<string, string?>(value, StringComparer.Ordinal));
        }
    }
}
