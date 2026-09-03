using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Hosting;

/// <summary>
/// Defines the contract for iapplication host builder.
/// </summary>
public interface IApplicationHostBuilder
{
    /// <summary>
    /// Gets the configuration value.
    /// </summary>
    IConfigurationManager Configuration { get; }

    /// <summary>
    /// Executes the configure services operation.
    /// </summary>
    IApplicationHostBuilder ConfigureServices(Action<IServiceCollection> configureServices);

    /// <summary>
    /// Executes the configure host operation.
    /// </summary>
    IApplicationHostBuilder ConfigureHost(Action<ApplicationHostOptions> configureOptions);

    /// <summary>
    /// Executes the build operation.
    /// </summary>
    IApplicationHost Build();
}
