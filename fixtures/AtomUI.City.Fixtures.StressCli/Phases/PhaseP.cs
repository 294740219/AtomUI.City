using AtomUI.City.Data;
using AtomUI.City.Fixtures.StressCli.DataIntegration;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Routing;
using AtomUI.City.Fixtures.StressCli.ViewModels;
using AtomUI.City.Localization;
using AtomUI.City.Mvvm;
using AtomUI.City.Routing;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

public static class PhaseP
{
    public static async Task RunAsync(StressExecutionOptions options, CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var scenario = await StressDataScenario.StartAsync(cancellationToken).ConfigureAwait(false);
        var services = scenario.Services;
        var state = services.GetRequiredService<IApplicationState>();
        var operations = services.GetRequiredService<IStressRemoteOperations>();
        var localization = services.GetRequiredService<ILocalizationService>();
        var descriptorCatalog = services.GetRequiredService<DataClientDescriptorCatalog>();

        using var routeScope = services.CreateScope();
        var routeResult = await routeScope.ServiceProvider.GetRequiredService<IRouter>()
            .NavigateAsync(FixtureRoutes.RemoteData(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        Record(
            "P01-route",
            "Router 导航到 RemoteOperationsViewModel",
            routeResult.Status == NavigationResultStatus.Success,
            routeResult.Status.ToString());

        var viewModel = services.GetRequiredService<RemoteOperationsViewModel>();
        await viewModel.ActivateAsync(new ActivationScope(), cancellationToken).ConfigureAwait(false);
        try
        {
            var descriptorOk = descriptorCatalog.TryGet(StressRemoteOperations.ClientId, out var descriptor)
                && descriptor!.Operations.Count == 4
                && descriptor.Operations.Any(operation => operation.CacheEnabled)
                && descriptor.Operations.Any(operation => operation.ConcurrencyPolicy == DataConcurrencyPolicy.LatestWins);
            Record("P02-generated", "Generator 产生并注册 4 个 Data operation descriptor", descriptorOk);

            var first = await viewModel.LoadProductAsync("SKU-0001", cancellationToken).ConfigureAwait(false);
            var second = await viewModel.LoadProductAsync("SKU-0001", cancellationToken).ConfigureAwait(false);
            Record(
                "P03-cache",
                "MVVM 命令经 Data pipeline 命中缓存且投影到 State",
                first.Succeeded && second.Succeeded
                    && scenario.Server.Backend.CountOf("get-product") == 1
                    && state.Get(PhaseD.StateCatalog.RemoteProductsLoaded).Value == 2
                    && state.Get(PhaseD.StateCatalog.RemoteInventory).Value == scenario.Server.Backend.Quantity,
                $"server={scenario.Server.Backend.CountOf("get-product")} projected={state.Get(PhaseD.StateCatalog.RemoteProductsLoaded).Value}");

            var order = await viewModel.SubmitOrderAsync("SKU-0001", 2, cancellationToken).ConfigureAwait(false);
            var afterMutation = await viewModel.LoadProductAsync("SKU-0001", cancellationToken).ConfigureAwait(false);
            Record(
                "P04-mutation",
                "mutation 乐观更新、确认和缓存失效形成闭环",
                order.Succeeded && afterMutation.Succeeded
                    && state.Get(PhaseD.StateCatalog.RemotePendingOptimistic).Value == 0
                    && state.Get(PhaseD.StateCatalog.RemoteOrdersSubmitted).Value == 1
                    && state.Get(PhaseD.StateCatalog.RemoteRevenue).Value == 40m
                    && scenario.Server.Backend.CountOf("get-product") == 2,
                $"pending={state.Get(PhaseD.StateCatalog.RemotePendingOptimistic).Value} server={scenario.Server.Backend.CountOf("get-product")}");

            scenario.Server.Backend.FailNext("get-product", 2);
            var retried = await operations.GetProductAsync(
                "SKU-RETRY",
                resilience: new DataResilienceOptions
                {
                    MaxRetryAttempts = 2,
                    RetryDelay = TimeSpan.FromMilliseconds(5),
                    PolicyName = "stress-transient",
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            Record(
                "P05-resilience",
                "瞬态 503 经统一 resilience 重试后恢复",
                retried.Succeeded && scenario.Server.Backend.CountOf("get-product") == 5,
                $"status={retried.Status} totalServerCalls={scenario.Server.Backend.CountOf("get-product")}");

            var english = viewModel.Message;
            var cultureResult = await localization.SetCultureAsync("zh-CN", cancellationToken).ConfigureAwait(false);
            var localizedLoad = await viewModel.LoadProductAsync("SKU-ZH", cancellationToken).ConfigureAwait(false);
            Record(
                "P06-localization",
                "Data 结果文案随 Localization culture 切换",
                cultureResult.Succeeded && localizedLoad.Succeeded
                    && !string.Equals(english, viewModel.Message, StringComparison.Ordinal)
                    && viewModel.Message.Contains("商品", StringComparison.Ordinal),
                viewModel.Message);

            var slow = viewModel.SearchAsync("obsolete", 500, cancellationToken);
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            await viewModel.DeactivateAsync(cancellationToken).ConfigureAwait(false);
            var cancelled = await slow.ConfigureAwait(false);
            Record(
                "P07-scope-cancel",
                "ViewModel 停用取消在途 Data 请求且不提交陈旧 UI 结果",
                cancelled.Status is DataResultStatus.Cancelled or DataResultStatus.StaleSuppressed
                    && viewModel.ActivationState == ActivationState.Deactivated,
                cancelled.Status.ToString());

            await viewModel.ActivateAsync(new ActivationScope(), cancellationToken).ConfigureAwait(false);
            var completed = 0;
            for (var index = 0; index < options.DataIterations; index++)
            {
                var result = await operations.GetProductAsync(
                    $"SKU-{index % 32:D4}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    completed++;
                }
            }

            var probe = services.GetRequiredService<IStressDataRequestProbe>();
            Record(
                "P08-soak",
                $"{options.DataIterations} 轮请求由缓存前置短路，重试复用 operationId",
                completed == options.DataIterations
                    && probe.InvocationCount > 0
                    && probe.InvocationCount < options.DataIterations + 20
                    && probe.OperationIds.Count > 0
                    && probe.OperationIds.Count <= probe.InvocationCount,
                $"completed={completed} handlers={probe.InvocationCount} ids={probe.OperationIds.Count}");

            var principal = await operations.GetPrincipalAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            Record(
                "P09-security",
                "Security token provider 只经请求头传递，业务状态不持有 token",
                principal.Succeeded
                    && principal.Value!.Principal == "user-a"
                    && !string.Join('|', new[]
                    {
                        state.Get(PhaseD.StateCatalog.RemotePrincipal).Value,
                        state.Get(PhaseD.StateCatalog.RemoteMessage).Value,
                        state.Get(PhaseD.StateCatalog.RemoteStatus).Value,
                    }).Contains("stress/", StringComparison.Ordinal),
                principal.Value?.Principal);
        }
        finally
        {
            if (viewModel.ActivationState == ActivationState.Active)
            {
                await viewModel.DeactivateAsync(CancellationToken.None).ConfigureAwait(false);
            }

            viewModel.Dispose();
        }
    }

    private static void Record(string id, string description, bool passed, string? detail = null) =>
        FixtureState.Report.Record(id, description, passed, detail);
}
