namespace AtomUI.City.Core.Diagnostics;

public interface IHostDiagnostics
{
    IReadOnlyList<HostDiagnosticRecord> Records { get; }

    void Write(HostDiagnosticRecord record);
}
