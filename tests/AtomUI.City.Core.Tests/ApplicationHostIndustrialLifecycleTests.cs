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
    public async Task ConcurrentStopCallersShareOnePublishedTransaction()
    {
        BlockingShutdownModule.Reset();
        var builder = ApplicationHost.CreateBuilder();
        builder.UseModule<BlockingShutdownModule>();
        builder.ConfigureHost(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));
        await using var host = builder.Build();
        await host.StartAsync();

        var firstStop = host.StopAsync();
        await BlockingShutdownModule.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var secondStop = host.StopAsync();

        Assert.Same(firstStop, secondStop);

        BlockingShutdownModule.Release.SetResult();
        await Task.WhenAll(firstStop, secondStop);
        Assert.True(BlockingShutdownModule.Completed);
    }

    [Fact]
    public async Task RecursiveStopFromStopMiddlewareFailsFastAndStillCleansUp()
    {
        IApplicationHost? host = null;
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStop, (_, next) =>
            {
                host!.StopAsync();
                return next();
            }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedStopProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedStopProbe>());
        });
        host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedStopProbe>();
        await host.StartAsync();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Contains(
            failure.Flatten().InnerExceptions,
            exception => exception is InvalidOperationException &&
                         exception.Message.Contains("cannot be invoked recursively", StringComparison.Ordinal));
        Assert.Equal(1, hostedProbe.StopCount);
        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [Fact]
    public async Task RecursiveStopFromApplicationStoppingCallbackFailsFast()
    {
        var builder = ApplicationHost.CreateBuilder();
        await using var host = builder.Build();
        await host.StartAsync();
        var applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        Exception? recursiveFailure = null;
        using var registration = applicationLifetime.ApplicationStopping.Register(() =>
            recursiveFailure = Record.Exception(() => host.StopAsync().GetAwaiter().GetResult()));

        await host.StopAsync();

        var failure = Assert.IsType<InvalidOperationException>(recursiveFailure);
        Assert.Contains("cannot be invoked recursively", failure.Message, StringComparison.Ordinal);
        Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
    }

    [Fact]
    public async Task RecursiveStartFromStartMiddlewareFailsFastAndRollsBack()
    {
        IApplicationHost? host = null;
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStart, (_, next) =>
            {
                host!.StartAsync();
                return next();
            }));
        host = builder.Build();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.Contains("cannot be invoked recursively", failure.Message, StringComparison.Ordinal);
        await host.StopAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task RecursiveDisposeFromStopMiddlewareFailsFastAndStillCleansUp()
    {
        IApplicationHost? host = null;
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureLifecycle(lifecycle =>
            lifecycle.Use(LifecycleStages.ApplicationStop, (_, next) =>
            {
                host!.DisposeAsync();
                return next();
            }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedStopProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedStopProbe>());
        });
        host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedStopProbe>();
        await host.StartAsync();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Contains(
            failure.Flatten().InnerExceptions,
            exception => exception is InvalidOperationException &&
                         exception.Message.Contains("cannot be invoked recursively", StringComparison.Ordinal));
        Assert.Equal(1, hostedProbe.StopCount);
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [Fact]
    public async Task RecursiveModuleRegistryShutdownFailsFastAndStillDisposesModules()
    {
        ReentrantShutdownModule.Reset();
        var builder = ApplicationHost.CreateBuilder();
        builder.UseModule<ReentrantShutdownModule>();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HostedStopProbe>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<HostedStopProbe>());
        });
        var host = builder.Build();
        var hostedProbe = host.Services.GetRequiredService<HostedStopProbe>();
        await host.StartAsync();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => host.StopAsync());

        Assert.Contains(
            failure.Flatten().InnerExceptions,
            exception => exception is InvalidOperationException &&
                         exception.Message.Contains("cannot be invoked recursively", StringComparison.Ordinal));
        Assert.True(ReentrantShutdownModule.WasDisposed);
        Assert.Equal(1, hostedProbe.StopCount);
        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
    }

    [Fact]
    public async Task RecursiveModuleRegistryDisposeFailsFastWithoutDuplicatingDisposal()
    {
        ReentrantRegistryDisposeModule.Reset();
        var builder = ApplicationHost.CreateBuilder();
        builder.UseModule<ReentrantRegistryDisposeModule>();
        await using var host = builder.Build();
        await host.StartAsync();

        await host.StopAsync();

        var failure = Assert.IsType<InvalidOperationException>(
            ReentrantRegistryDisposeModule.RecursiveFailure);
        Assert.Contains("cannot be invoked recursively", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, ReentrantRegistryDisposeModule.DisposeCount);
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

    private sealed class ReentrantShutdownModule : ModuleBase, IDisposable
    {
        public static bool WasDisposed { get; private set; }

        public static void Reset() => WasDisposed = false;

        public override async ValueTask OnApplicationShutdownAsync(
            ApplicationShutdownContext context,
            CancellationToken cancellationToken = default)
        {
            var registry = context.Services.GetRequiredService<IModuleRegistry>();
            await registry.ShutdownAsync(
                context.ApplicationContext,
                context.Services,
                cancellationToken);
        }

        public void Dispose() => WasDisposed = true;
    }

    private sealed class ReentrantRegistryDisposeModule : ModuleBase, IDisposable
    {
        private static IAsyncDisposable? _registry;

        public static Exception? RecursiveFailure { get; private set; }

        public static int DisposeCount { get; private set; }

        public static void Reset()
        {
            _registry = null;
            RecursiveFailure = null;
            DisposeCount = 0;
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            _registry = Assert.IsAssignableFrom<IAsyncDisposable>(
                context.Services.GetRequiredService<IModuleRegistry>());
        }

        public void Dispose()
        {
            DisposeCount++;
            RecursiveFailure = Record.Exception(() =>
                _registry!.DisposeAsync().AsTask().GetAwaiter().GetResult());
        }
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
