namespace AtomUI.City.Templates;

public sealed class ApplicationTemplateOptions
{
    private static readonly HashSet<string> ReservedIdentifiers = new(StringComparer.Ordinal)
    {
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "case",
        "catch",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while",
    };

    public required string AppName { get; init; }

    public required string RootNamespace { get; init; }

    public required string OutputPath { get; init; }

    public string TargetFramework { get; init; } = "net10.0";

    public bool IncludeTests { get; init; } = true;

    public bool UseAot { get; init; }

    public bool UseDynamicPlugins { get; init; }

    public bool IncludeSample { get; init; }

    public string EffectiveRootNamespace => string.IsNullOrWhiteSpace(RootNamespace)
        ? AppName
        : RootNamespace;

    public IReadOnlyList<TemplateDiagnostic> Validate()
    {
        var diagnostics = new List<TemplateDiagnostic>();
        if (!IsValidIdentifier(AppName))
        {
            diagnostics.Add(CreateVariableDiagnostic(
                "AUCTPL0001",
                "AppName must be a valid C# identifier.",
                "appName",
                AppName,
                "csharp-identifier"));
        }

        var rootNamespace = EffectiveRootNamespace;
        if (rootNamespace.StartsWith("AtomUI.City", StringComparison.Ordinal))
        {
            diagnostics.Add(CreateVariableDiagnostic(
                "AUCTPL0002",
                "RootNamespace must not start with 'AtomUI.City'.",
                "rootNamespace",
                rootNamespace,
                "reserved-framework-namespace"));
        }
        else if (!IsValidNamespace(rootNamespace))
        {
            diagnostics.Add(CreateVariableDiagnostic(
                "AUCTPL0001",
                "RootNamespace must be a valid C# namespace.",
                "rootNamespace",
                rootNamespace,
                "csharp-namespace"));
        }

        if (!IsValidTargetFramework(TargetFramework))
        {
            diagnostics.Add(CreateVariableDiagnostic(
                "AUCTPL0001",
                "TargetFramework must be a framework moniker, not a path segment.",
                "targetFramework",
                TargetFramework,
                "target-framework-moniker"));
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            diagnostics.Add(CreateVariableDiagnostic(
                "AUCTPL0001",
                "OutputPath is required.",
                "outputPath",
                OutputPath,
                "non-empty-path"));
        }

        if (UseAot && UseDynamicPlugins)
        {
            diagnostics.Add(CreateVariableDiagnostic(
                "AUCTPL0301",
                "UseAot cannot be combined with UseDynamicPlugins by default.",
                "useDynamicPlugins",
                UseDynamicPlugins.ToString().ToLowerInvariant(),
                "aot-dynamic-plugin-conflict"));
        }

        return Array.AsReadOnly(diagnostics.ToArray());
    }

    private static TemplateDiagnostic CreateVariableDiagnostic(
        string code,
        string message,
        string variable,
        string rawValue,
        string rule)
    {
        return new TemplateDiagnostic(
            code,
            message,
            new Dictionary<string, object?>
            {
                ["variable"] = variable,
                ["rawValue"] = rawValue,
                ["rule"] = rule,
            });
    }

    private static bool IsValidNamespace(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Split('.').All(IsValidIdentifier);
    }

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            ReservedIdentifiers.Contains(value) ||
            !(value[0] == '_' || char.IsLetter(value[0])))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var current = value[index];
            if (current != '_' && !char.IsLetterOrDigit(current))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidTargetFramework(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !value.Contains("..", StringComparison.Ordinal) &&
            !value.Contains('/', StringComparison.Ordinal) &&
            !value.Contains('\\', StringComparison.Ordinal) &&
            value.All(static current => char.IsLetterOrDigit(current) || current is '.' or '-');
    }
}
