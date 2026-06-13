using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;

namespace AtomUI.City.Testing;

public sealed class SourceGenerationTestResult
{
    internal SourceGenerationTestResult(
        GeneratedSourceSnapshot snapshot,
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyList<Diagnostic> compilationDiagnostics)
    {
        Snapshot = snapshot;
        Diagnostics = new ReadOnlyCollection<Diagnostic>(diagnostics.ToArray());
        CompilationDiagnostics = new ReadOnlyCollection<Diagnostic>(compilationDiagnostics.ToArray());
    }

    public GeneratedSourceSnapshot Snapshot { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public IReadOnlyList<Diagnostic> CompilationDiagnostics { get; }
}
