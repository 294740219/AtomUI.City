using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.Core.Tests;

public sealed class ApplicationHostRuntimeTests
{
    [Fact]
    public async Task StartAndStopAsyncAreIdempotentForHostedServices()
    {
        var builder = ApplicationHost.CreateBuilder();

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<RuntimeProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<RuntimeProbe>());
        });

        await using var host = builder.Build();
        var probe = host.Services.GetRequiredService<RuntimeProbe>();

        await host.StartAsync();
        await host.StartAsync();
        await host.StopAsync();
        await host.StopAsync();

        Assert.Equal(1, probe.StartCount);
        Assert.Equal(1, probe.StopCount);
    }

    [Fact]
    public async Task ApplicationContextIsAvailableFromRuntimeServices()
    {
        var builder = ApplicationHost.CreateBuilder();

        await using var host = builder.Build();

        Assert.Same(host.Context, host.Services.GetRequiredService<IApplicationContext>());
        Assert.Same(host.Context, host.Services.GetRequiredService<ApplicationContext>());
    }

    [Fact]
    public async Task StartAfterStopIsRejectedAndDisposeStillSucceeds()
    {
        var host = ApplicationHost.CreateBuilder().Build();

        await host.StartAsync();
        await host.StopAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
        await host.DisposeAsync();
    }

    [Fact]
    public async Task StopBeforeStartPermanentlyClosesTheHost()
    {
        var host = ApplicationHost.CreateBuilder().Build();

        await host.StopAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
        await host.DisposeAsync();
    }

    [Fact]
    public async Task StartupFailureIsRecordedAndHostCanBeDisposed()
    {
        var builder = ApplicationHost.CreateBuilder();

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IHostedService, FailingHostedService>();
        });

        var host = builder.Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostStartFailed &&
            record.Context["exceptionType"] == typeof(InvalidOperationException).FullName);

        await host.StopAsync();
        await host.DisposeAsync();
    }

    private sealed class RuntimeProbe : IHostedService
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCount++;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class FailingHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("startup failed");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
