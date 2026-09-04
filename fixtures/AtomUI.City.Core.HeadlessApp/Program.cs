using AtomUI.City.Core.Hosting;
using AtomUI.City.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.Core.HeadlessApp;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        return ProcessEntryPoint.RunAsync(() => RunAsync(args));
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args is ["--test-entry-failure", var phase])
        {
            return await RunEntryFailureAsync(args, phase);
        }

        if (args is ["--test-entry-hang"])
        {
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return 0;
        }

        if (args is ["--test-scenario", var scenario])
        {
            return await HeadlessTestScenarios.RunAsync(scenario);
        }

        var builder = ApplicationHost.CreateBuilder(args);
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.Core.HeadlessApp";
            options.ApplicationName = "AtomUI.City.Core.HeadlessApp";
        });
        await using var host = builder.Build();

        Console.WriteLine("Starting...");
        await host.StartAsync();

        Console.WriteLine("Stopping...");
        await host.StopAsync();

        Console.WriteLine("Stopped...");
        return 0;
    }

    private static async Task<int> RunEntryFailureAsync(string[] args, string phase)
    {
        var builder = ApplicationHost.CreateBuilder(args);
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.Core.HeadlessApp.Failure";
            options.ApplicationName = "AtomUI.City.Core.HeadlessApp.Failure";
        });

        switch (phase)
        {
            case "build":
                builder.ConfigureServices(_ =>
                    throw new InvalidOperationException("Core fixture build failure."));
                break;
            case "di":
                builder.ConfigureServices(services =>
                    services.AddHostedService<MissingDependencyHostedService>());
                break;
            case "start":
                builder.ConfigureServices(services =>
                    services.AddHostedService<ThrowingHostedService>());
                break;
            default:
                throw new ArgumentException($"Unknown entry failure phase '{phase}'.", nameof(phase));
        }

        await using var host = builder.Build();
        await host.StartAsync();
        await host.StopAsync();
        return 0;
    }

    private interface IMissingDependency;

    private sealed class MissingDependencyHostedService(IMissingDependency dependency) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            GC.KeepAlive(dependency);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowingHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Core fixture start failure.");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
