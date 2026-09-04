using AtomUI.City.Core.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TodoCli;

/// <summary>
/// Reusable host construction so the test project can boot the same host the
/// real CLI uses.
/// </summary>
public static class TodoHost
{
    public static IApplicationHostBuilder CreateBuilder(string[] args)
    {
        var builder = ApplicationHost.CreateBuilder(args);
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "TodoCli";
            options.ApplicationName = "TodoCli";
            options.ShutdownTimeout = TimeSpan.FromSeconds(10);
        });
        builder.Configuration.AddEnvironmentVariables(prefix: "TODOCLI_");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(args);
            services.Configure<TodoOptions>(builder.Configuration.GetSection("Todo"));
            services.AddSingleton<CliRunner>();
            services.AddHostedService(sp => sp.GetRequiredService<CliRunner>());
        });

        return builder;
    }
}
