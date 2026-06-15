namespace AtomUI.City.Cli;

public sealed record CliDiagnostic(
    string Code,
    string Message,
    string Severity,
    string? SuggestedAction = null,
    string? DocumentationLink = null)
{
    public string? Target { get; init; }

    public int? Position { get; init; }

    public static CliDiagnostic Error(
        string code,
        string message,
        string? target = null,
        int? position = null)
    {
        return new CliDiagnostic(code, message, "Error")
        {
            Target = target,
            Position = position,
        };
    }
}
