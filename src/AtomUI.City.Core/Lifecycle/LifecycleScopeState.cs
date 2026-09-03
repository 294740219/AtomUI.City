namespace AtomUI.City.Core.Lifecycle;

/// <summary>
/// Defines the supported values for lifecycle scope state.
/// </summary>
public enum LifecycleScopeState
{
    /// <summary>
    /// Represents the created option.
    /// </summary>
    Created,
    /// <summary>
    /// Represents the starting option.
    /// </summary>
    Starting,
    /// <summary>
    /// Represents the running option.
    /// </summary>
    Running,
    /// <summary>
    /// Represents the cancel requested option.
    /// </summary>
    CancelRequested,
    /// <summary>
    /// Represents the stopping option.
    /// </summary>
    Stopping,
    /// <summary>
    /// Represents the stopped option.
    /// </summary>
    Stopped,
    /// <summary>
    /// Represents the faulted option.
    /// </summary>
    Faulted,
    /// <summary>
    /// Represents the unload pending option.
    /// </summary>
    UnloadPending,
    /// <summary>
    /// Represents the disposing option.
    /// </summary>
    Disposing,
    /// <summary>
    /// Represents the disposed option.
    /// </summary>
    Disposed,
}
