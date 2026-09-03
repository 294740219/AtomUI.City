using AtomUI.City.Core.Hosting;

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
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);

        ApplicationContext = applicationContext;
        Services = services;
    }

    /// <summary>
    /// Gets the application context value.
    /// </summary>
    public IApplicationContext ApplicationContext { get; }

    /// <summary>
    /// Gets the services value.
    /// </summary>
    public IServiceProvider Services { get; }
}
