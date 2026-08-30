using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.Core.Tests;

public sealed class ApplicationHostIndustrialLifecycleTests
{
    [Fact]
    public async Task LifecyclePipelineWrapsHostAndModuleTransactions()
    {
        var calls = new List<string>();
        var builder = ApplicationHost.CreateBuilder();

        builder.ConfigureLifecycle(lifecycle =>
        {
            foreach (var stage in new[]
                     {
                         LifecycleStages.ApplicationStart,
                         LifecycleStages.ModuleInitialize,
                         LifecycleStages.ModuleStart,
                         LifecycleStages.ApplicationStop,
                         LifecycleStages.ModuleStop,
                     })
            {
                lifecycle.Use(stage, async (_, next) =>
                {
                    calls.Add($"{stage.Key}:before");
                    await next();
                    calls.Add($"{stage.Key}:after");
                });
            }
        });

        await using var host = builder.Build();

        Assert.NotNull(host.Services.GetRequiredService<LifecyclePipeline>());

        await host.StartAsync();
        await host.StopAsync();

        Assert.Equal(
            [
                "Application.Start:before",
                "Module.Initialize:before",
                "Module.Initialize:after",
                "Module.Start:before",
                "Module.Start:after",
                "Application.Start:after",
                "Application.Stop:before",
                "Module.Stop:before",
                "Module.Stop:after",
                "Application.Stop:after",
            ],
            calls);
    }

    [Fact]
    public async Task InitializationFailureRollsBackEnteredModulesInReverseOrder()
    {
        RollbackRecorder.Reset();
        var builder = ApplicationHost.CreateBuilder();
        builder.UseModule<FailingApplicationModule>();
        builder.UseModule<FoundationModule>();
        var host = builder.Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Equal("module initialization failed", exception.Message);
        Assert.Equal(
            ["foundation:init", "application:init", "application:shutdown", "foundation:shutdown"],
            RollbackRecorder.Calls);

        await host.StopAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task ShutdownContinuesAfterModuleFailuresAndStopsGenericHost()
    {
        ShutdownRecorder.Reset();
        var builder = ApplicationHost.CreateBuilder();
        builder.UseModule<SecondFailingShutdownModule>();
        builder.UseModule<FirstFailingShutdownModule>();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedStopProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedStopProbe>());
        });

        var host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedStopProbe>();
        await host.StartAsync();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Equal(2, failure.Flatten().InnerExceptions.Count);
        Assert.Equal(["second:shutdown", "first:shutdown"], ShutdownRecorder.Calls);
        Assert.Equal(1, hostedProbe.StopCount);
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [Fact]
    public async Task ModuleContextsUseApplicationServiceScopeAndDisposeItOnStop()
    {
        ScopedModule.Reset();
        var builder = ApplicationHost.CreateBuilder();
        builder.UseModule<ScopedModule>();
        builder.ConfigureServices(services => services.AddScoped<ScopedProbe>());

        await using var host = builder.Build();
        await host.StartAsync();

        Assert.NotNull(ScopedModule.InitializedInstance);
        Assert.False(ScopedModule.InitializedInstance.IsDisposed);

        await host.StopAsync();

        Assert.Same(ScopedModule.InitializedInstance, ScopedModule.ShutdownInstance);
        Assert.True(ScopedModule.InitializedInstance.IsDisposed);
    }

    [Fact]
    public async Task CancelingConcurrentStopWaitDoesNotCancelCleanupTransaction()
    {
        BlockingShutdownModule.Reset();
        var builder = ApplicationHost.CreateBuilder();
        builder.UseModule<BlockingShutdownModule>();
        builder.ConfigureHost(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));
        await using var host = builder.Build();
        await host.StartAsync();

        var stop = host.StopAsync();
        await BlockingShutdownModule.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.StopAsync(cancellation.Token));

        BlockingShutdownModule.Release.SetResult();
        await stop;
        Assert.True(BlockingShutdownModule.Completed);
    }

    [Fact]
    public async Task StopMiddlewareFailureDoesNotSkipRequiredCleanup()
    {
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStop, static (_, _) =>
                ValueTask.FromException(new InvalidOperationException("stop middleware failed"))));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedStopProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedStopProbe>());
        });
        var host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedStopProbe>();
        await host.StartAsync();

        await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Equal(1, hostedProbe.StopCount);
        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [Fact]
    public async Task ShutdownTimeoutCancelsCooperativeModuleAndStillDisposesIt()
    {
        TimedShutdownModule.Reset();
        var builder = ApplicationHost.CreateBuilder();
        builder.UseModule<TimedShutdownModule>();
        builder.ConfigureHost(options => options.ShutdownTimeout = TimeSpan.FromMilliseconds(50));
        var host = builder.Build();
        await host.StartAsync();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Contains(failure.Flatten().InnerExceptions, exception => exception is TimeoutException);
        Assert.True(TimedShutdownModule.WasDisposed);
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [DependsOn(typeof(FoundationModule))]
    private sealed class FailingApplicationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            RollbackRecorder.Record("application:init");
            throw new InvalidOperationException("module initialization failed");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            RollbackRecorder.Record("application:shutdown");
        }
    }

    private sealed class FoundationModule : ModuleBase
    {
        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            RollbackRecorder.Record("foundation:init");
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            RollbackRecorder.Record("foundation:shutdown");
        }
    }

    private sealed class FirstFailingShutdownModule : ModuleBase
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            ShutdownRecorder.Record("first:shutdown");
            throw new InvalidOperationException("first shutdown failed");
        }
    }

    [DependsOn(typeof(FirstFailingShutdownModule))]
    private sealed class SecondFailingShutdownModule : ModuleBase
    {
        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            ShutdownRecorder.Record("second:shutdown");
            throw new InvalidOperationException("second shutdown failed");
        }
    }

    private sealed class ScopedModule : ModuleBase
    {
        public static ScopedProbe? InitializedInstance { get; private set; }

        public static ScopedProbe? ShutdownInstance { get; private set; }

        public static void Reset()
        {
            InitializedInstance = null;
            ShutdownInstance = null;
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            InitializedInstance = context.Services.GetRequiredService<ScopedProbe>();
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            ShutdownInstance = context.Services.GetRequiredService<ScopedProbe>();
        }
    }

    private sealed class ScopedProbe : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class HostedStopProbe : IHostedService
    {
        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingShutdownModule : ModuleBase
    {
        public static TaskCompletionSource Entered { get; private set; } = CreateCompletionSource();

        public static TaskCompletionSource Release { get; private set; } = CreateCompletionSource();

        public static bool Completed { get; private set; }

        public static void Reset()
        {
            Entered = CreateCompletionSource();
            Release = CreateCompletionSource();
            Completed = false;
        }

        public override async ValueTask OnApplicationShutdownAsync(
            ApplicationShutdownContext context,
            CancellationToken cancellationToken = default)
        {
            Entered.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            Completed = true;
        }

        private static TaskCompletionSource CreateCompletionSource()
        {
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private sealed class TimedShutdownModule : ModuleBase, IDisposable
    {
        public static bool WasDisposed { get; private set; }

        public static void Reset() => WasDisposed = false;

        public override async ValueTask OnApplicationShutdownAsync(
            ApplicationShutdownContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public void Dispose() => WasDisposed = true;
    }

    private static class RollbackRecorder
    {
        private static readonly List<string> Values = [];

        public static IReadOnlyList<string> Calls => Values;

        public static void Record(string value) => Values.Add(value);

        public static void Reset() => Values.Clear();
    }

    private static class ShutdownRecorder
    {
        private static readonly List<string> Values = [];

        public static IReadOnlyList<string> Calls => Values;

        public static void Record(string value) => Values.Add(value);

        public static void Reset() => Values.Clear();
    }
}
