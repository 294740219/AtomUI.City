using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents contribution configuration context.
/// </summary>
public sealed class ContributionConfigurationContext
{
    /// <summary>
    /// Initializes a new instance of the contribution configuration context class.
    /// </summary>
    public ContributionConfigurationContext(
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
