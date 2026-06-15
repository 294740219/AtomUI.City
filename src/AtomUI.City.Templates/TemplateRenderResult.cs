using System.Collections.ObjectModel;

namespace AtomUI.City.Templates;

public sealed class TemplateRenderResult
{
    private TemplateRenderResult(TemplatePlan? plan, IReadOnlyList<TemplateDiagnostic> diagnostics)
    {
        Plan = plan;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public TemplatePlan? Plan { get; }

    public IReadOnlyList<TemplateDiagnostic> Diagnostics { get; }

    public bool Succeeded => Diagnostics.Count == 0;

    public static TemplateRenderResult Success(TemplatePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new TemplateRenderResult(plan, []);
    }

    public static TemplateRenderResult Failed(params TemplateDiagnostic[] diagnostics)
    {
        return new TemplateRenderResult(null, diagnostics);
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
