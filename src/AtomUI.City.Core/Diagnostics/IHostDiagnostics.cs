namespace AtomUI.City.Core.Diagnostics;

/// <summary>
/// Defines the contract for ihost diagnostics.
/// </summary>
public interface IHostDiagnostics
{
    /// <summary>
    /// Gets the records value.
    /// </summary>
    IReadOnlyList<HostDiagnosticRecord> Records { get; }

    /// <summary>
    /// Executes the write operation.
    /// </summary>
    void Write(HostDiagnosticRecord record);

    /// <summary>
    /// Executes the complete operation.
    /// </summary>
    void Complete();
}
