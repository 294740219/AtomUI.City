namespace AtomUI.City.Core.Lifecycle;

/// <summary>
/// Defines the supported values for lifecycle scope kind.
/// </summary>
public enum LifecycleScopeKind
{
    /// <summary>
    /// Represents the host option.
    /// </summary>
    Host,
    /// <summary>
    /// Represents the application option.
    /// </summary>
    Application,
    /// <summary>
    /// Represents the presentation option.
    /// </summary>
    Presentation,
    /// <summary>
    /// Represents the window option.
    /// </summary>
    Window,
    /// <summary>
    /// Represents the navigation option.
    /// </summary>
    Navigation,
    /// <summary>
    /// Represents the route option.
    /// </summary>
    Route,
    /// <summary>
    /// Represents the activation option.
    /// </summary>
    Activation,
    /// <summary>
    /// Represents the state option.
    /// </summary>
    State,
    /// <summary>
    /// Represents the operation option.
    /// </summary>
    Operation,
    /// <summary>
    /// Represents the subscription option.
    /// </summary>
    Subscription,
}
