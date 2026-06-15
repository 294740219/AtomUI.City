using System.Collections.ObjectModel;

namespace AtomUI.City.Templates;

public sealed class TemplatePlan
{
    public TemplatePlan(
        string operationId,
        string command,
        IReadOnlyDictionary<string, object?> inputs,
        IReadOnlyList<TemplateChange> changes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        OperationId = operationId;
        Command = command;
        Inputs = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(inputs, StringComparer.Ordinal));
        Changes = Array.AsReadOnly(changes.ToArray());
    }

    public string SchemaVersion { get; } = "1.0";

    public string OperationId { get; }

    public string Command { get; }

    public IReadOnlyDictionary<string, object?> Inputs { get; }

    public IReadOnlyList<TemplateChange> Changes { get; }

    public IReadOnlyList<string> BuildTargets { get; } = [];

    public IReadOnlyList<string> TestTargets { get; } = [];

    public IReadOnlyList<string> DocsRequired { get; } = [];

    public IReadOnlyList<string> Risks { get; } = [];

    public IReadOnlyList<string> Rollback { get; } = [];

    public IReadOnlyList<TemplateDiagnostic> Validate()
    {
        var diagnostics = new List<TemplateDiagnostic>();
        var normalizedPaths = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var change in Changes)
        {
            if (!string.Equals(change.Type, "create", StringComparison.Ordinal))
            {
                diagnostics.Add(new TemplateDiagnostic(
                    "AUCTPL1003",
                    "Template change type is not supported.",
                    new Dictionary<string, object?>
                    {
                        ["type"] = change.Type,
                        ["path"] = change.Path,
                    }));
            }

            if (!TemplateChange.TryNormalizePath(change.Path, out var normalizedPath, out var error))
            {
                diagnostics.Add(new TemplateDiagnostic(
                    "AUCTPL1001",
                    error,
                    new Dictionary<string, object?>
                    {
                        ["path"] = change.Path,
                    }));
                continue;
            }

            if (normalizedPaths.TryGetValue(normalizedPath, out var firstPath))
            {
                diagnostics.Add(new TemplateDiagnostic(
                    "AUCTPL1002",
                    "Template plan contains a duplicate output path.",
                    new Dictionary<string, object?>
                    {
                        ["path"] = change.Path,
                        ["firstPath"] = firstPath,
                        ["normalizedPath"] = normalizedPath,
                    }));
                continue;
            }

            normalizedPaths.Add(normalizedPath, change.Path);
        }

        return Array.AsReadOnly(diagnostics.ToArray());
    }
}
