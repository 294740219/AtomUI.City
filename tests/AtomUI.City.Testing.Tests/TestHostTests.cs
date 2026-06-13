using AtomUI.City.Hosting;
using AtomUI.City.Testing;

namespace AtomUI.City.Testing.Tests;

public sealed class TestHostTests
{
    [Fact]
    public void CreateBuilderBuildsHostWithDefaultRuntimeFakes()
    {
        using var host = TestHost
            .CreateBuilder()
            .UseProperty("environment", "test")
            .Build();

        Assert.IsType<ApplicationContext>(host.ApplicationContext);
        Assert.Equal("test", host.ApplicationContext.Properties["environment"]);
        Assert.True(Directory.Exists(host.Directory.RootPath));
        Assert.NotNull(host.Dispatcher);
        Assert.NotNull(host.Scheduler);
        Assert.NotNull(host.Diagnostics);
    }

    [Fact]
    public void BuildFreezesBuilderMutationEntrypoints()
    {
        var builder = TestHost.CreateBuilder()
            .UseProperty("environment", "test");

        using var host = builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.UseProperty("next", "value"));
        Assert.Throws<InvalidOperationException>(() => builder.UseDirectoryName("next"));
        Assert.Throws<InvalidOperationException>(() => builder.KeepDirectoryOnDispose());
        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal("test", host.ApplicationContext.Properties["environment"]);
    }

    [Fact]
    public async Task StopAsyncIsIdempotent()
    {
        await using var host = TestHost.CreateBuilder().Build();

        await host.StopAsync();
        await host.StopAsync();

        Assert.True(host.IsStopped);
    }

    [Fact]
    public void DisposeRemovesTestDirectory()
    {
        string rootPath;

        using (var host = TestHost.CreateBuilder().Build())
        {
            rootPath = host.Directory.RootPath;

            Assert.True(Directory.Exists(rootPath));
        }

        Assert.False(Directory.Exists(rootPath));
    }

    [Fact]
    public async Task DisposeStopsRuntimeFakesAndRejectsMutableUse()
    {
        var host = TestHost.CreateBuilder().Build();

        await host.DisposeAsync();

        Assert.True(host.IsStopped);
        Assert.True(host.Diagnostics.Contains("AUCTEST001"));
        Assert.Throws<ObjectDisposedException>(() => host.Diagnostics.Add("AUCTEST999", "after dispose"));
        Assert.Throws<ObjectDisposedException>(() => host.Dispatcher.Post(() => { }));
        Assert.Throws<ObjectDisposedException>(() => host.Scheduler.Schedule(TimeSpan.Zero, () => { }));
    }
}
