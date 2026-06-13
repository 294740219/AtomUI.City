namespace AtomUI.City.Testing;

public sealed class AotCompatibilityCheck
{
    private readonly List<ForbiddenAotPattern> _forbiddenPatterns = [];

    private AotCompatibilityCheck()
    {
    }

    public static AotCompatibilityCheck Create()
    {
        return new AotCompatibilityCheck();
    }

    public AotCompatibilityCheck ForbidPattern(string diagnosticId, string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        if (!_forbiddenPatterns.Any(existing =>
                string.Equals(existing.DiagnosticId, diagnosticId, StringComparison.Ordinal)
                && string.Equals(existing.Pattern, pattern, StringComparison.Ordinal)))
        {
            _forbiddenPatterns.Add(new ForbiddenAotPattern(diagnosticId, pattern));
        }

        return this;
    }

    public AotCompatibilityCheck ForbidDefaultAotPatterns()
    {
        ForbidPattern("AOT001", "Assembly.GetTypes");
        ForbidPattern("AOT002", "Activator.CreateInstance");
        ForbidPattern("AOT003", "DynamicMethod");

        return this;
    }

    public IReadOnlyList<AotCompatibilityDiagnostic> Evaluate(
        IEnumerable<SourceFile> sources,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var diagnostics = new List<AotCompatibilityDiagnostic>();

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var pattern in _forbiddenPatterns)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (source.Text.Contains(pattern.Pattern, StringComparison.Ordinal))
                {
                    diagnostics.Add(new AotCompatibilityDiagnostic(
                        pattern.DiagnosticId,
                        source.Path,
                        $"Source '{source.Path}' uses forbidden AOT pattern '{pattern.Pattern}'."));
                }
            }
        }

        return Array.AsReadOnly(diagnostics.ToArray());
    }

    private sealed record ForbiddenAotPattern(string DiagnosticId, string Pattern);
}
