namespace AtomUI.City.Core.Lifecycle;

/// <summary>
/// Represents lifecycle stages.
/// </summary>
public static class LifecycleStages
{
    private static readonly IReadOnlyList<LifecycleStage> Stages;

    /// <summary>
    /// Identifies the application start value.
    /// </summary>
    public static readonly LifecycleStage ApplicationStart = new(LifecycleStageArea.Application, "Start");
    /// <summary>
    /// Identifies the application suspend value.
    /// </summary>
    public static readonly LifecycleStage ApplicationSuspend = new(LifecycleStageArea.Application, "Suspend");
    /// <summary>
    /// Identifies the application resume value.
    /// </summary>
    public static readonly LifecycleStage ApplicationResume = new(LifecycleStageArea.Application, "Resume");
    /// <summary>
    /// Identifies the application stop value.
    /// </summary>
    public static readonly LifecycleStage ApplicationStop = new(LifecycleStageArea.Application, "Stop");

    /// <summary>
    /// Identifies the module initialize value.
    /// </summary>
    public static readonly LifecycleStage ModuleInitialize = new(LifecycleStageArea.Module, "Initialize");
    /// <summary>
    /// Identifies the module start value.
    /// </summary>
    public static readonly LifecycleStage ModuleStart = new(LifecycleStageArea.Module, "Start");
    /// <summary>
    /// Identifies the module stop value.
    /// </summary>
    public static readonly LifecycleStage ModuleStop = new(LifecycleStageArea.Module, "Stop");

    /// <summary>
    /// Identifies the plugin load value.
    /// </summary>
    public static readonly LifecycleStage PluginLoad = new(LifecycleStageArea.Plugin, "Load");
    /// <summary>
    /// Identifies the plugin activate value.
    /// </summary>
    public static readonly LifecycleStage PluginActivate = new(LifecycleStageArea.Plugin, "Activate");
    /// <summary>
    /// Identifies the plugin deactivate value.
    /// </summary>
    public static readonly LifecycleStage PluginDeactivate = new(LifecycleStageArea.Plugin, "Deactivate");
    /// <summary>
    /// Identifies the plugin unload value.
    /// </summary>
    public static readonly LifecycleStage PluginUnload = new(LifecycleStageArea.Plugin, "Unload");

    /// <summary>
    /// Identifies the route navigate value.
    /// </summary>
    public static readonly LifecycleStage RouteNavigate = new(LifecycleStageArea.Route, "Navigate");
    /// <summary>
    /// Identifies the route enter value.
    /// </summary>
    public static readonly LifecycleStage RouteEnter = new(LifecycleStageArea.Route, "Enter");
    /// <summary>
    /// Identifies the route leave value.
    /// </summary>
    public static readonly LifecycleStage RouteLeave = new(LifecycleStageArea.Route, "Leave");

    /// <summary>
    /// Identifies the activation activate value.
    /// </summary>
    public static readonly LifecycleStage ActivationActivate = new(LifecycleStageArea.Activation, "Activate");
    /// <summary>
    /// Identifies the activation deactivate value.
    /// </summary>
    public static readonly LifecycleStage ActivationDeactivate = new(LifecycleStageArea.Activation, "Deactivate");

    /// <summary>
    /// Identifies the operation execute value.
    /// </summary>
    public static readonly LifecycleStage OperationExecute = new(LifecycleStageArea.Operation, "Execute");
    /// <summary>
    /// Identifies the operation cancel value.
    /// </summary>
    public static readonly LifecycleStage OperationCancel = new(LifecycleStageArea.Operation, "Cancel");
    /// <summary>
    /// Identifies the operation fail value.
    /// </summary>
    public static readonly LifecycleStage OperationFail = new(LifecycleStageArea.Operation, "Fail");

    /// <summary>
    /// Identifies the error handle value.
    /// </summary>
    public static readonly LifecycleStage ErrorHandle = new(LifecycleStageArea.Error, "Handle");

    /// <summary>
    /// Gets the all value.
    /// </summary>
    public static IReadOnlyList<LifecycleStage> All => Stages;

    static LifecycleStages()
    {
        Stages = Array.AsReadOnly(new[]
        {
            ApplicationStart,
            ApplicationSuspend,
            ApplicationResume,
            ApplicationStop,
            ModuleInitialize,
            ModuleStart,
            ModuleStop,
            PluginLoad,
            PluginActivate,
            PluginDeactivate,
            PluginUnload,
            RouteNavigate,
            RouteEnter,
            RouteLeave,
            ActivationActivate,
            ActivationDeactivate,
            OperationExecute,
            OperationCancel,
            OperationFail,
            ErrorHandle,
        });
    }
}
