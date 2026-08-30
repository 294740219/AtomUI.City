using System.Text.Json;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtomUI.City.Core.HeadlessApp;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var scenario = args.FirstOrDefault() ?? "lifecycle";

        try
        {
            var result = scenario switch
            {
                "lifecycle" => await RunLifecycleAsync(),
                "startup-failure" => await RunStartupFailureAsync(),
                "shutdown-failure" => await RunShutdownFailureAsync(),
                "run-cancellation" => await RunCancellationAsync(),
                _ => new { scenario, success = false, error = "unknown scenario" },
            };

            Console.WriteLine(JsonSerializer.Serialize(result));
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                scenario,
                success = false,
                errorType = exception.GetType().FullName,
                error = exception.Message,
            }));
            return 1;
        }
    }

    private static async Task<object> RunLifecycleAsync()
    {
        HeadlessRecorder.Reset();
        ScopedHeadlessModule.Reset();
        var builder = CreateBuilder();
        builder.UseModule<HeadlessApplicationModule>();
        builder.UseModule<HeadlessFoundationModule>();
        builder.UseModule<ScopedHeadlessModule>();
        builder.ConfigureServices(services =>
        {
            services.AddScoped<ScopedProbe>();
            services.AddSingleton<HostedProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedProbe>());
        });
        builder.ConfigureLifecycle(lifecycle =>
        {
            lifecycle.Use(LifecycleStages.ApplicationStart, async (_, next) =>
            {
                HeadlessRecorder.Record("application:start:before");
                await next();
                HeadlessRecorder.Record("application:start:after");
            });
            lifecycle.Use(LifecycleStages.ApplicationStop, async (_, next) =>
            {
                HeadlessRecorder.Record("application:stop:before");
                await next();
                HeadlessRecorder.Record("application:stop:after");
            });
        });

        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var hostedProbe = host.Services.GetRequiredService<HostedProbe>();
        await host.StartAsync();
        await host.StopAsync();
        await host.DisposeAsync();

        return new
        {
            scenario = "lifecycle",
            success = true,
            calls = HeadlessRecorder.Calls,
            hostedStartCount = hostedProbe.StartCount,
            hostedStopCount = hostedProbe.StopCount,
            scopedDisposed = ScopedHeadlessModule.Instance?.IsDisposed,
            hostScopeState = host.HostScope.State.ToString(),
            applicationScopeState = host.ApplicationScope?.State.ToString(),
            diagnostics = diagnostics.Records.Select(record => record.Code).ToArray(),
        };
    }

    private static async Task<object> RunStartupFailureAsync()
    {
        HeadlessRecorder.Reset();
        var builder = CreateBuilder();
        builder.UseModule<FailingStartupModule>();
        builder.UseModule<HeadlessFoundationModule>();
        var host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        Exception? startupFailure = null;

        try
        {
            await host.StartAsync();
        }
        catch (Exception exception)
        {
            startupFailure = exception;
        }

        await host.StopAsync();
        await host.DisposeAsync();

        return new
        {
            scenario = "startup-failure",
            success = startupFailure is InvalidOperationException,
            errorType = startupFailure?.GetType().FullName,
            error = startupFailure?.Message,
            calls = HeadlessRecorder.Calls,
            hostScopeState = host.HostScope.State.ToString(),
            diagnostics = diagnostics.Records.Select(record => record.Code).ToArray(),
        };
    }

    private static async Task<object> RunShutdownFailureAsync()
    {
        HeadlessRecorder.Reset();
        var builder = CreateBuilder();
        builder.UseModule<SecondFailingStopModule>();
        builder.UseModule<FirstFailingStopModule>();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedProbe>());
        });
        var host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedProbe>();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        await host.StartAsync();
        AggregateException? stopFailure = null;

        try
        {
            await host.StopAsync();
        }
        catch (AggregateException exception)
        {
            stopFailure = exception.Flatten();
        }

        try
        {
            await host.DisposeAsync();
        }
        catch (AggregateException)
        {
            // The repeated dispose observes the same completed cleanup transaction.
        }

        return new
        {
            scenario = "shutdown-failure",
            success = stopFailure?.InnerExceptions.Count == 2,
            failureCount = stopFailure?.InnerExceptions.Count,
            calls = HeadlessRecorder.Calls,
            hostedStopCount = hostedProbe.StopCount,
            hostScopeState = host.HostScope.State.ToString(),
            diagnostics = diagnostics.Records.Select(record => record.Code).ToArray(),
        };
    }

    private static async Task<object> RunCancellationAsync()
    {
        HeadlessRecorder.Reset();
        var builder = CreateBuilder();
        builder.UseModule<CancellationModule>();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedProbe>());
        });
        var host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedProbe>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var canceled = false;

        try
        {
            await host.RunAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        await host.DisposeAsync();

        return new
        {
            scenario = "run-cancellation",
            success = canceled && hostedProbe.StopCount == 1,
            canceled,
            calls = HeadlessRecorder.Calls,
            hostedStopCount = hostedProbe.StopCount,
            hostScopeState = host.HostScope.State.ToString(),
        };
    }

    private static IApplicationHostBuilder CreateBuilder()
    {
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureHost(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));
        builder.ConfigureServices(services =>
            services.AddLogging(logging => logging.ClearProviders()));
        return builder;
    }

    private sealed class HeadlessFoundationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            HeadlessRecorder.Record("foundation:init");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("foundation:shutdown");
        }
    }

    [DependsOn(typeof(HeadlessFoundationModule))]
    private sealed class HeadlessApplicationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            HeadlessRecorder.Record("application:init");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("application:shutdown");
        }
    }

    [DependsOn(typeof(HeadlessFoundationModule))]
    private sealed class FailingStartupModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            HeadlessRecorder.Record("failing:init");
            throw new InvalidOperationException("headless startup failed");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("failing:shutdown");
        }
    }

    private sealed class FirstFailingStopModule : ModuleBase
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("first:shutdown");
            throw new InvalidOperationException("first stop failed");
        }
    }

    [DependsOn(typeof(FirstFailingStopModule))]
    private sealed class SecondFailingStopModule : ModuleBase
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("second:shutdown");
            throw new InvalidOperationException("second stop failed");
        }
    }

    private sealed class ScopedHeadlessModule : ModuleBase
    {
        public static ScopedProbe? Instance { get; private set; }

        public static void Reset() => Instance = null;

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            Instance = context.Services.GetRequiredService<ScopedProbe>();
        }
    }

    private sealed class CancellationModule : ModuleBase
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            HeadlessRecorder.Record("cancellation:shutdown");
        }
    }

    private sealed class ScopedProbe : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class HostedProbe : IHostedService
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

    private static class HeadlessRecorder
    {
        private static readonly List<string> Values = [];

        public static IReadOnlyList<string> Calls => Values;

        public static void Record(string value) => Values.Add(value);

        public static void Reset() => Values.Clear();
    }
}
