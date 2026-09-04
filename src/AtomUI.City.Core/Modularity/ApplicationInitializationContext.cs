using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents application initialization context.
/// </summary>
public sealed class ApplicationInitializationContext
{
    /// <summary>
    /// Initializes a new instance of the application initialization context class.
    /// </summary>
    public ApplicationInitializationContext(
        IApplicationContext applicationContext,
        IServiceProvider services,
        LifecycleScope applicationScope)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(applicationScope);

        ApplicationContext = applicationContext;
        Services = services;
        ApplicationScope = applicationScope;
    }

    /// <summary>
    /// Gets the application context value.
    /// </summary>
    public IApplicationContext ApplicationContext { get; }

    /// <summary>
    /// Gets the services value.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the Host-owned application lifecycle scope.
    /// </summary>
    public LifecycleScope ApplicationScope { get; }
}
