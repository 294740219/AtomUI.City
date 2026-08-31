using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Hosting;

public interface IApplicationHostBuilder
{
    IConfigurationManager Configuration { get; }

    IDictionary<string, object?> Properties { get; }

    IApplicationHostBuilder ConfigureServices(Action<IServiceCollection> configureServices);

    IApplicationHostBuilder ConfigureHost(Action<ApplicationHostOptions> configureOptions);

    IApplicationHost Build();
}
