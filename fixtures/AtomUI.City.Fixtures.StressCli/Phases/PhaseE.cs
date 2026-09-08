using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.Events;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.ViewModels;
using AtomUI.City.Mvvm;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>Phase E：16 个 ViewModel 的激活、命令、交互和订阅释放矩阵。</summary>
public static class PhaseE
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken);

        var eventBus = host.Services.GetRequiredService<IEventBus>();
        var state = host.Services.GetRequiredService<IApplicationState>();
        var writer = host.Services.GetRequiredService<IApplicationStateWriter>();
        var registry = host.Services.GetRequiredService<IStateRegistry>();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
        var owner = host.Services.GetRequiredService<LifecycleScope>();

        var viewModels = CreateViewModels(eventBus, owner, state, writer, diagnostics);
        if (viewModels.Count != StressCliProgram.ViewModelCount)
        {
            throw new InvalidOperationException($"ViewModel catalog contains {viewModels.Count} entries.");
        }

        foreach (var viewModel in viewModels)
        {
            registry.Add(StateDefinition.Create(
                viewModel.StateKey,
                0,
                access: StateAccessPolicy.HostWrite));
        }

        for (var round = 0; round < 4; round++)
        {
            foreach (var viewModel in viewModels)
            {
                await viewModel.ActivateAsync(
                    new ActivationContext(new ActivationScope(diagnostics), $"phase-e-{round}"),
                    cancellationToken).ConfigureAwait(false);
                await viewModel.DeactivateAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var viewModel in viewModels)
        {
            await viewModel.ActivateAsync(
                new ActivationContext(new ActivationScope(diagnostics), "phase-e-final"),
                cancellationToken).ConfigureAwait(false);
        }

        var activationOk = viewModels.All(viewModel =>
            viewModel.IsActive &&
            viewModel.CountOf("activated") == 5 &&
            viewModel.CurrentActivationScope is ActivationScope { IsDisposed: false });
        FixtureState.Report.Record(
            "I10-activation",
            "16 个 ViewModel 完成五轮激活且最终 scope 有效",
            activationOk,
            activationOk ? null : "Activation count/state/scope mismatch.");

        foreach (var viewModel in viewModels)
        {
            viewModel.SeedState(viewModel.Name.Length);
            await viewModel.RefreshCommand.ExecuteAsync(null).ConfigureAwait(false);
            viewModel.FailCommand.Execute(null);
        }

        var shell = viewModels.Single(viewModel => viewModel.Name == "Shell");
        var concurrentCommands = Enumerable.Range(0, 8)
            .Select(_ => shell.RefreshCommand.ExecuteAsync(null))
            .ToArray();
        await Task.WhenAll(concurrentCommands).ConfigureAwait(false);

        var commandsOk = viewModels.All(viewModel =>
                             viewModel.CountOf("refresh") >= 1 &&
                             viewModel.FailState.LastResult?.Status == OperationStatus.Failed) &&
                         shell.RefreshState.RejectedExecutionCount >= 7;
        FixtureState.Report.Record(
            "I10-commands",
            "16 个成功命令、16 个失败命令及并发拒绝均被跟踪",
            commandsOk,
            $"shellRejected={shell.RefreshState.RejectedExecutionCount}");

        var interactionFailures = new List<string>();
        foreach (var viewModel in viewModels)
        {
            var scope = viewModel.CurrentActivationScope!;
            var registration = viewModel.Confirm.RegisterHandler(
                (request, token) => ValueTask.FromResult(request.Request == viewModel.Name),
                scope);
            var completed = await viewModel.Confirm.RequestAsync(viewModel.Name, cancellationToken).ConfigureAwait(false);
            registration.Dispose();
            var notHandled = await viewModel.Confirm.RequestAsync("released", cancellationToken).ConfigureAwait(false);

            if (completed.Status != InteractionResultStatus.Completed || !completed.Value ||
                notHandled.Status != InteractionResultStatus.NotHandled)
            {
                interactionFailures.Add(viewModel.Name);
            }
        }

        FixtureState.Report.Record(
            "I10-interactions",
            "16 个交互完成后释放 handler，后续请求均 NotHandled",
            interactionFailures.Count == 0,
            interactionFailures.Count == 0 ? null : string.Join(", ", interactionFailures));

        var before = viewModels.ToDictionary(
            viewModel => viewModel.Name,
            viewModel => (State: viewModel.CountOf("state-reaction"), Event: viewModel.CountOf("event")),
            StringComparer.Ordinal);

        foreach (var viewModel in viewModels)
        {
            await viewModel.DeactivateAsync(cancellationToken).ConfigureAwait(false);
            writer.Set(viewModel.StateKey, viewModel.Name.Length + 100);
        }

        await eventBus.PublishAsync(new SettingsChanged("post-deactivate"), cancellationToken: cancellationToken);
        await Task.Delay(80, cancellationToken).ConfigureAwait(false);

        var leaks = viewModels
            .Where(viewModel =>
                viewModel.CountOf("state-reaction") != before[viewModel.Name].State ||
                viewModel.CountOf("event") != before[viewModel.Name].Event)
            .Select(viewModel => viewModel.Name)
            .ToArray();

        FixtureState.Report.Record(
            "I11-release",
            "16 个 ViewModel 停用后 State/EventBus 回调均冻结",
            leaks.Length == 0,
            leaks.Length == 0 ? null : string.Join(", ", leaks));

        foreach (var viewModel in viewModels)
        {
            viewModel.Dispose();
        }

        await host.StopAsync(cancellationToken);
    }

    internal static IReadOnlyList<FixtureViewModelBase> CreateViewModels(
        IEventBus eventBus,
        LifecycleScope owner,
        IApplicationState state,
        IApplicationStateWriter writer,
        IHostDiagnostics diagnostics) =>
    [
        new ShellViewModel(eventBus, owner, state, writer, diagnostics),
        new DashboardViewModel(eventBus, owner, state, writer, diagnostics),
        new OrdersViewModel(eventBus, owner, state, writer, diagnostics),
        new InventoryViewModel(eventBus, owner, state, writer, diagnostics),
        new CustomersViewModel(eventBus, owner, state, writer, diagnostics),
        new BillingViewModel(eventBus, owner, state, writer, diagnostics),
        new ReportViewModel(eventBus, owner, state, writer, diagnostics),
        new NotificationsViewModel(eventBus, owner, state, writer, diagnostics),
        new AuditViewModel(eventBus, owner, state, writer, diagnostics),
        new SettingsViewModel(eventBus, owner, state, writer, diagnostics),
        new ProductsViewModel(eventBus, owner, state, writer, diagnostics),
        new PaymentsViewModel(eventBus, owner, state, writer, diagnostics),
        new FulfillmentViewModel(eventBus, owner, state, writer, diagnostics),
        new SearchViewModel(eventBus, owner, state, writer, diagnostics),
        new SupportViewModel(eventBus, owner, state, writer, diagnostics),
        new WorkflowViewModel(eventBus, owner, state, writer, diagnostics),
    ];
}
