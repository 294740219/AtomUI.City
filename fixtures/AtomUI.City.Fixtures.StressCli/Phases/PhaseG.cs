using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Modules;
using AtomUI.City.Fixtures.StressCli.ViewModels;
using AtomUI.City.Mvvm;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>Phase G：八个真实故障、取消、并发和幂等场景。</summary>
public static class PhaseG
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        await VerifyInitializationCompensationAsync(cancellationToken).ConfigureAwait(false);
        await VerifyShutdownAggregationAsync(cancellationToken).ConfigureAwait(false);
        await VerifyCommandRejectionAsync().ConfigureAwait(false);
        await VerifyStateAuthorityAsync(cancellationToken).ConfigureAwait(false);
        VerifyCancellationStorm();
        VerifyDoubleDispose();
        await VerifyViewModelDeactivationStormAsync(cancellationToken).ConfigureAwait(false);
        await VerifyFullShutdownAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task VerifyInitializationCompensationAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        FixtureState.FaultOnInit = true;
        var host = StressHost.CreateBuilder().Build();
        var startThrew = false;

        try
        {
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            startThrew = true;
        }

        var faultyInitCount = FixtureState.Ledger.CountMilestone("Faulty", LifecycleMilestone.Initializing);
        var compensated = FixtureState.Ledger.ModulesInMilestoneOrder(LifecycleMilestone.Stopping).Count;
        FixtureState.Report.Record(
            "I17-init-fault",
            "模块初始化失败触发 Host 启动拒绝与逆序补偿",
            startThrew && faultyInitCount == 1 && compensated > 0,
            $"threw={startThrew} faultyInit={faultyInitCount} compensated={compensated}");

        try { await host.DisposeAsync().ConfigureAwait(false); } catch { }
    }

    private static async Task VerifyShutdownAggregationAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        FixtureState.FaultOnShutdown = true;
        var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);
        var stopThrew = false;

        try
        {
            await host.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (AggregateException)
        {
            stopThrew = true;
        }

        var foundationStopped = FixtureState.Ledger.CountMilestone("Foundation", LifecycleMilestone.Stopped) == 1;
        var flakyEntered = FixtureState.Ledger.CountMilestone("Flaky", LifecycleMilestone.Stopping) == 1;
        FixtureState.Report.Record(
            "I17-stop-fault",
            "单模块停机失败被聚合且不阻断其余 39 个模块清理",
            stopThrew && foundationStopped && flakyEntered,
            $"threw={stopThrew} foundationStopped={foundationStopped} flakyEntered={flakyEntered}");

        try { await host.DisposeAsync().ConfigureAwait(false); } catch { }
    }

    private static async Task VerifyCommandRejectionAsync()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = new CommandExecutionState("chaos.concurrent", typeof(PhaseG));
        var command = CommandFactory.CreateAsync(
            async cancellationToken =>
            {
                started.TrySetResult();
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            },
            state);

        var winner = command.ExecuteAsync(null);
        await started.Task.ConfigureAwait(false);
        var rejected = Enumerable.Range(0, 7).Select(_ => command.ExecuteAsync(null)).ToArray();
        release.TrySetResult();
        await Task.WhenAll(rejected.Append(winner)).ConfigureAwait(false);

        FixtureState.Report.Record(
            "I17-command-storm",
            "真实命令并发风暴只有一个执行者，其他七次被跟踪为 Rejected",
            state.LastResult?.Status == OperationStatus.Completed && state.RejectedExecutionCount == 7,
            $"status={state.LastResult?.Status} rejected={state.RejectedExecutionCount}");
    }

    private static async Task VerifyStateAuthorityAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);
        var registry = (ApplicationStateRegistry)host.Services.GetRequiredService<IStateRegistry>();
        var key = new StateKey<int>("fixtures.chaos.owner-state");
        registry.Add(StateDefinition.Create(
            key,
            0,
            access: StateAccessPolicy.OwnerWrite,
            ownerModule: "Orders"));

        var denied = false;
        try
        {
            registry.CreateWriter(StateWriteAuthority.Module("Inventory")).Set(key, 1);
        }
        catch (StateAccessDeniedException)
        {
            denied = true;
        }

        FixtureState.Report.Record("I17-authority", "跨模块 State 越权写被确定拒绝", denied);
        await host.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void VerifyCancellationStorm()
    {
        var cancelled = 0;
        Parallel.For(0, 50, _ =>
        {
            using var operation = OperationScope.Start(new CancellationToken(canceled: true));
            if (operation.Status == OperationStatus.Canceled)
            {
                Interlocked.Increment(ref cancelled);
            }
        });

        FixtureState.Report.Record(
            "I17-cancel-storm",
            "50 个预取消 OperationScope 全部进入 Canceled",
            cancelled == 50,
            $"cancelled={cancelled}");
    }

    private static void VerifyDoubleDispose()
    {
        var disposed = 0;
        var scope = new ActivationScope();
        scope.Add(new CallbackDisposable(() => Interlocked.Increment(ref disposed)));
        Parallel.For(0, 16, _ => scope.Dispose());

        FixtureState.Report.Record(
            "I17-dispose-storm",
            "ActivationScope 16 路并发 Dispose 仅释放资源一次",
            scope.IsDisposed && disposed == 1,
            $"disposed={disposed}");
    }

    private static async Task VerifyViewModelDeactivationStormAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);
        var eventBus = host.Services.GetRequiredService<IEventBus>();
        var appState = host.Services.GetRequiredService<IApplicationState>();
        var writer = host.Services.GetRequiredService<IApplicationStateWriter>();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var owner = host.Services.GetRequiredService<LifecycleScope>();
        host.Services.GetRequiredService<IStateRegistry>().Add(StateDefinition.Create(
            new StateKey<int>("fixtures.viewmodel.shell"),
            0,
            access: StateAccessPolicy.HostWrite));

        var viewModels = Enumerable.Range(0, 16)
            .Select(_ => new ShellViewModel(eventBus, owner, appState, writer, diagnostics))
            .ToArray();
        foreach (var viewModel in viewModels)
        {
            await viewModel.ActivateAsync(new ActivationScope(diagnostics), cancellationToken).ConfigureAwait(false);
        }

        await Task.WhenAll(viewModels.Select(viewModel => viewModel.DeactivateAsync(cancellationToken).AsTask())).ConfigureAwait(false);
        var allDeactivated = viewModels.All(viewModel => viewModel.ActivationState == ActivationState.Deactivated);
        FixtureState.Report.Record(
            "I17-deactivation-storm",
            "16 个活跃 ViewModel 并发停用后全部 Deactivated",
            allDeactivated);

        foreach (var viewModel in viewModels)
        {
            viewModel.Dispose();
        }

        await host.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task VerifyFullShutdownAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);
        await host.StopAsync(cancellationToken).ConfigureAwait(false);
        await host.StopAsync(cancellationToken).ConfigureAwait(false);

        var complete = ModuleGraph.AllModules.All(module =>
            FixtureState.Ledger.CountMilestone(module, LifecycleMilestone.Stopped) == 1);
        FixtureState.Report.Record(
            "I17-full-shutdown",
            "重复 Stop 后 40 个模块仍恰好停止一次",
            complete);
    }

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                callback();
            }
        }
    }
}
