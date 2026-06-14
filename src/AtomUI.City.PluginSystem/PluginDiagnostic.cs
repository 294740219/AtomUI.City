namespace AtomUI.City.PluginSystem;

public sealed record PluginDiagnostic(
    string Code,
    string Message,
    string? PluginId = null,
    string? Field = null,
    string? Path = null)
{
    public string Code { get; init; } = !string.IsNullOrWhiteSpace(Code)
        ? Code
        : throw new ArgumentException("Plugin diagnostic code cannot be empty.", nameof(Code));

    public string Message { get; init; } = !string.IsNullOrWhiteSpace(Message)
        ? Message
        : throw new ArgumentException("Plugin diagnostic message cannot be empty.", nameof(Message));
}

public sealed class PluginValidationResult
{
    public PluginValidationResult(IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; }

    public bool Succeeded => Diagnostics.Count == 0;

    public static PluginValidationResult Success { get; } = new([]);
}
