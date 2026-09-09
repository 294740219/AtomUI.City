using System.Collections.ObjectModel;

namespace AtomUI.City.Templates;

public sealed class TemplatePlan
{
    public TemplatePlan(
        string operationId,
        string command,
        IReadOnlyDictionary<string, object?> inputs,
        IReadOnlyList<TemplateChange> changes)
        : this(operationId, command, inputs, changes, [], [], [], [], [])
    {
    }

    public TemplatePlan(
        string operationId,
        string command,
        IReadOnlyDictionary<string, object?> inputs,
        IReadOnlyList<TemplateChange> changes,
        IReadOnlyList<string> buildTargets,
        IReadOnlyList<string> testTargets,
        IReadOnlyList<string> docsRequired,
        IReadOnlyList<string> risks,
        IReadOnlyList<string> rollback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(buildTargets);
        ArgumentNullException.ThrowIfNull(testTargets);
        ArgumentNullException.ThrowIfNull(docsRequired);
        ArgumentNullException.ThrowIfNull(risks);
        ArgumentNullException.ThrowIfNull(rollback);
        if (changes.Any(static change => change is null))
        {
            throw new ArgumentException("Template changes cannot contain null entries.", nameof(changes));
        }

        OperationId = operationId;
        Command = command;
        Inputs = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(inputs, StringComparer.Ordinal));
        Changes = Array.AsReadOnly(changes.ToArray());
        BuildTargets = Array.AsReadOnly(buildTargets.ToArray());
        TestTargets = Array.AsReadOnly(testTargets.ToArray());
        DocsRequired = Array.AsReadOnly(docsRequired.ToArray());
        Risks = Array.AsReadOnly(risks.ToArray());
        Rollback = Array.AsReadOnly(rollback.ToArray());
    }

    public string SchemaVersion { get; } = "1.0";

    public string OperationId { get; }

    public string Command { get; }

    public IReadOnlyDictionary<string, object?> Inputs { get; }

    public IReadOnlyList<TemplateChange> Changes { get; }

    public IReadOnlyList<string> BuildTargets { get; }

    public IReadOnlyList<string> TestTargets { get; }

    public IReadOnlyList<string> DocsRequired { get; }

    public IReadOnlyList<string> Risks { get; }

    public IReadOnlyList<string> Rollback { get; }

    public IReadOnlyList<TemplateDiagnostic> Validate()
    {
        var diagnostics = new List<TemplateDiagnostic>();
        var normalizedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
