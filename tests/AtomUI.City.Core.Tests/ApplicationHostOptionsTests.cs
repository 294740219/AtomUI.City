using AtomUI.City.Core.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AtomUI.City.Core.Tests;

public sealed class ApplicationHostOptionsTests
{
    [Fact]
    public async Task ConfigureHostRegistersApplicationHostOptions()
    {
        var builder = ApplicationHost.CreateBuilder();

        builder.ConfigureHost(options =>
        {
            options.ApplicationName = "Sample.Desktop";
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
            options.AllowDynamicDiscovery = true;
        });

        await using var host = builder.Build();

        var options = host.Services.GetRequiredService<IOptions<ApplicationHostOptions>>().Value;

        Assert.Equal("Sample.Desktop", options.ApplicationName);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ShutdownTimeout);
        Assert.True(options.AllowDynamicDiscovery);
        Assert.Equal("Sample.Desktop", host.Context.ApplicationName);
    }

    [Fact]
    public void ApplicationHostOptionsExposeConservativeDefaults()
    {
        var options = new ApplicationHostOptions();

        Assert.Null(options.ApplicationName);
        Assert.Equal(TimeSpan.FromSeconds(30), options.ShutdownTimeout);
        Assert.Equal(1024, options.DiagnosticsCapacity);
        Assert.False(options.AllowDynamicDiscovery);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildRejectsInvalidDiagnosticsCapacity(int capacity)
    {
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureHost(options => options.DiagnosticsCapacity = capacity);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build());
    }

    [Fact]
    public void BuildRejectsNonPositiveShutdownTimeout()
    {
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureHost(options => options.ShutdownTimeout = TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build());
    }
}
