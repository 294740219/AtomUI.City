using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
namespace AtomUICityApplication.Tests;

public sealed class ApplicationSmokeTests
{
    [Fact]
    public async Task ApplicationHostStartsAndStops()
    {
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUICityApplication.Tests";
            options.ApplicationName = "AtomUICityApplication Tests";
        });

        await using var host = builder.Build();
        await host.StartAsync();
        Assert.Equal(LifecycleScopeState.Running, host.HostScope.State);

        await host.StopAsync();
        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
    }
}
