using System.Collections.ObjectModel;

namespace AtomUI.City.Templates;

public sealed class TemplateRenderResult
{
    private TemplateRenderResult(
        TemplatePlan? plan,
        IReadOnlyList<TemplateDiagnostic> diagnostics,
        IReadOnlyList<string> appliedPaths)
    {
        Plan = plan;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        AppliedPaths = Array.AsReadOnly(appliedPaths.ToArray());
    }

    public TemplatePlan? Plan { get; }

    public IReadOnlyList<TemplateDiagnostic> Diagnostics { get; }

    public IReadOnlyList<string> AppliedPaths { get; }

    public bool Succeeded => Plan is not null && Diagnostics.Count == 0;

    public static TemplateRenderResult Success(TemplatePlan plan, IReadOnlyList<string>? appliedPaths = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Validate().Count > 0)
        {
            throw new ArgumentException("A successful template result requires a valid plan.", nameof(plan));
        }

        var plannedPaths = plan.Changes.Select(static change => change.Path).ToArray();
        var resolvedAppliedPaths = appliedPaths?.ToArray() ?? plannedPaths;
        if (!plannedPaths.SequenceEqual(resolvedAppliedPaths, StringComparer.Ordinal))
        {
            throw new ArgumentException("Applied paths must exactly match the template plan.", nameof(appliedPaths));
        }

        return new TemplateRenderResult(
            plan,
            [],
            resolvedAppliedPaths);
    }

    public static TemplateRenderResult Failed(params TemplateDiagnostic[] diagnostics)
    {
        return Failed(null, diagnostics);
    }

    public static TemplateRenderResult Failed(TemplatePlan? plan, params TemplateDiagnostic[] diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (diagnostics.Length == 0)
        {
            throw new ArgumentException("A failed template result must contain at least one diagnostic.", nameof(diagnostics));
        }

        if (diagnostics.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Template diagnostics cannot contain null entries.", nameof(diagnostics));
        }

        return new TemplateRenderResult(plan, diagnostics, []);
    }
}

public sealed record TemplateDiagnostic
{
    public TemplateDiagnostic(string code, string message)
        : this(code, message, new Dictionary<string, object?>())
    {
    }

    public TemplateDiagnostic(
        string code,
        string message,
        IReadOnlyDictionary<string, object?> context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(context);

        Code = code;
        Message = message;
        Context = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(context, StringComparer.Ordinal));
    }

    public string Code { get; }

    public string Message { get; }

    public IReadOnlyDictionary<string, object?> Context { get; }
}
