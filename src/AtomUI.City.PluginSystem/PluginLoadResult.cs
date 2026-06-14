namespace AtomUI.City.PluginSystem;

public sealed class PluginLoadResult
{
    private PluginLoadResult(
        PluginRuntime? runtime,
        PluginRuntimeState state,
        IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        Runtime = runtime;
        State = state;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public PluginRuntime? Runtime { get; }

    public PluginRuntimeState State { get; }

    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; }

    public bool Succeeded => Runtime is not null && Diagnostics.Count == 0;

    public static PluginLoadResult Success(PluginRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        return new PluginLoadResult(runtime, PluginRuntimeState.Loaded, []);
    }

    public static PluginLoadResult Failed(IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        return new PluginLoadResult(null, PluginRuntimeState.Faulted, diagnostics);
    }
}
