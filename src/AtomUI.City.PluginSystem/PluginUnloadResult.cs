namespace AtomUI.City.PluginSystem;

public sealed class PluginUnloadResult
{
    private PluginUnloadResult(
        PluginRuntimeState state,
        IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        State = state;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public PluginRuntimeState State { get; }

    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; }

    public bool Succeeded => State == PluginRuntimeState.Unloaded && Diagnostics.Count == 0;

    public static PluginUnloadResult Success { get; } = new(PluginRuntimeState.Unloaded, []);

    public static PluginUnloadResult Pending(IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        return new PluginUnloadResult(PluginRuntimeState.UnloadPending, diagnostics);
    }
}
