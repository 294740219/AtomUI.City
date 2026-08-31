using System.Text.Json;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtomUI.City.Core.HeadlessApp;

internal static class HeadlessTestScenarios
{
    public static async Task<int> RunAsync(string scenario)
    {
        try
        {
            var result = scenario switch
            {
                "lifecycle" => await RunLifecycleAsync(),
                "startup-failure" => await RunStartupFailureAsync(),
                "shutdown-failure" => await RunShutdownFailureAsync(),
                "run-cancellation" => await RunCancellationAsync(),
                "concurrent-stop" => await RunConcurrentStopAsync(),
                "reentrant-stop" => await RunReentrantStopAsync(),
                "service-ordering" => await RunServiceOrderingAsync(),
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

    private static async Task<object> RunConcurrentStopAsync()
    {
        ConcurrentStopModule.Reset();
        var builder = CreateBuilder();
        builder.UseModule<ConcurrentStopModule>();
        var host = builder.Build();
        await host.StartAsync();

        var firstStop = host.StopAsync();
        await ConcurrentStopModule.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var secondStop = host.StopAsync();
        var sharedTransaction = ReferenceEquals(firstStop, secondStop);

        ConcurrentStopModule.Release.SetResult();
        await Task.WhenAll(firstStop, secondStop);
        await host.DisposeAsync();

        return new
        {
            scenario = "concurrent-stop",
            success = sharedTransaction && ConcurrentStopModule.ShutdownCount == 1,
            sharedTransaction,
            shutdownCount = ConcurrentStopModule.ShutdownCount,
            hostScopeState = host.HostScope.State.ToString(),
        };
    }

    private static async Task<object> RunReentrantStopAsync()
    {
        IApplicationHost? host = null;
        var builder = CreateBuilder();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStop, (_, next) =>
            {
                host!.StopAsync();
                return next();
            }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedProbe>());
        });
        host = builder.Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var hostedProbe = host.Services.GetRequiredService<HostedProbe>();
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

        var hostScopeStateAfterStop = host.HostScope.State.ToString();

        try
        {
            await host.DisposeAsync();
        }
        catch (AggregateException)
        {
            // Disposal observes the failed stop transaction after completing final cleanup.
        }

        var recursiveFailure = stopFailure?.InnerExceptions.Any(exception =>
            exception is InvalidOperationException &&
            exception.Message.Contains("cannot be invoked recursively", StringComparison.Ordinal)) == true;

        return new
        {
            scenario = "reentrant-stop",
            success = recursiveFailure &&
                      hostedProbe.StopCount == 1 &&
                      hostScopeStateAfterStop == LifecycleScopeState.Stopped.ToString(),
            recursiveFailure,
            hostedStopCount = hostedProbe.StopCount,
            hostScopeStateAfterStop,
            diagnostics = diagnostics.Records.Select(record => record.Code).ToArray(),
        };
    }

    private static async Task<object> RunServiceOrderingAsync()
    {
        HeadlessServiceOrderingModule.Reset();
        var builder = CreateBuilder();
        builder.UseModule<HeadlessServiceOrderingModule>();
        builder.ConfigureServices(services =>
        {
            HeadlessServiceOrderingModule.Record("user:first");
            services.AddSingleton<IHeadlessOrderedService, UserHeadlessOrderedService>();
        });
        builder.ConfigureServices(_ => HeadlessServiceOrderingModule.Record("user:second"));
        var deferred = HeadlessServiceOrderingModule.Calls.Count == 0;
        var host = builder.Build();
        var resolvedType = host.Services.GetRequiredService<IHeadlessOrderedService>().GetType().Name;
        var registeredTypes = host.Services
            .GetServices<IHeadlessOrderedService>()
            .Select(service => service.GetType().Name)
            .ToArray();
        await host.DisposeAsync();

        var expectedCalls = new[]
        {
            "module:pre",
            "module:configure",
            "module:post",
            "user:first",
            "user:second",
        };

        return new
        {
            scenario = "service-ordering",
            success = deferred &&
                      resolvedType == nameof(UserHeadlessOrderedService) &&
                      HeadlessServiceOrderingModule.Calls.SequenceEqual(expectedCalls),
            deferred,
            resolvedType,
            registeredTypes,
            calls = HeadlessServiceOrderingModule.Calls,
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

    private sealed class ConcurrentStopModule : ModuleBase
    {
        public static TaskCompletionSource Entered { get; private set; } = CreateCompletionSource();

        public static TaskCompletionSource Release { get; private set; } = CreateCompletionSource();

        public static int ShutdownCount { get; private set; }

        public static void Reset()
        {
            Entered = CreateCompletionSource();
            Release = CreateCompletionSource();
            ShutdownCount = 0;
        }

        public override async ValueTask OnApplicationShutdownAsync(
            ApplicationShutdownContext context,
            CancellationToken cancellationToken = default)
        {
            ShutdownCount++;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }

        private static TaskCompletionSource CreateCompletionSource()
        {
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private interface IHeadlessOrderedService;

    private sealed class ModuleHeadlessOrderedService : IHeadlessOrderedService;

    private sealed class UserHeadlessOrderedService : IHeadlessOrderedService;

    private sealed class HeadlessServiceOrderingModule : ModuleBase
    {
        private static readonly List<string> RecordedCalls = [];

        public static IReadOnlyList<string> Calls => RecordedCalls;

        public static void Reset() => RecordedCalls.Clear();

        public static void Record(string call) => RecordedCalls.Add(call);

        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            Record("module:pre");
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Record("module:configure");
            context.Services.AddSingleton<IHeadlessOrderedService, ModuleHeadlessOrderedService>();
        }

        public override void PostConfigureServices(ServiceConfigurationContext context)
        {
            Record("module:post");
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
