using System.Collections.Concurrent;
using System.Net.Http;
using AtomUI.City.Data;
using AtomUI.City.Fixtures.StressCli.DataIntegration;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

public static class PhaseR
{
    public static async Task RunAsync(StressExecutionOptions options, CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var scenario = await StressDataScenario.StartAsync(cancellationToken).ConfigureAwait(false);
        var services = scenario.Services;
        var operations = services.GetRequiredService<IStressRemoteOperations>();
        var state = services.GetRequiredService<IApplicationState>();
        var writer = services.GetRequiredService<IApplicationStateWriter>();
        var failures = new ConcurrentQueue<string>();
        var succeeded = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, options.Operations),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = options.Workers,
                CancellationToken = cancellationToken,
            },
            async (index, token) =>
            {
                var result = await operations.GetProductAsync(
                    $"SKU-{index % 64:D4}",
                    cancellationToken: token).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    Interlocked.Increment(ref succeeded);
                }
                else
                {
                    failures.Enqueue($"{index}:{result.Status}:{result.Error?.Kind}");
                }
            }).ConfigureAwait(false);

        Record(
            "R01-parallel",
            $"{options.Operations} 次并行请求在 {options.Workers} workers 下全部收束",
            succeeded == options.Operations && failures.IsEmpty,
            $"success={succeeded} failed={failures.Count} transport={scenario.Server.Backend.CountOf("get-product")}");

        var firstSearch = operations.SearchAsync(
            new StressSearchRequest("old", 300),
            DataConcurrencyPolicy.LatestWins,
            cancellationToken: cancellationToken).AsTask();
        await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        var latestSearch = operations.SearchAsync(
            new StressSearchRequest("latest", 1),
            DataConcurrencyPolicy.LatestWins,
            cancellationToken: cancellationToken).AsTask();
        var searchResults = await Task.WhenAll(firstSearch, latestSearch).ConfigureAwait(false);
        Record(
            "R02-latest-wins",
            "并发搜索只允许最新结果提交",
            searchResults[0].Status is DataResultStatus.Cancelled or DataResultStatus.StaleSuppressed
                && searchResults[1].Succeeded
                && searchResults[1].Value == "user-a:latest",
            $"old={searchResults[0].Status} latest={searchResults[1].Status}");

        writer.Set(PhaseD.StateCatalog.RemoteInventory, scenario.Server.Backend.Quantity);
        var beforeRollback = state.Get(PhaseD.StateCatalog.RemoteInventory).Value;
        scenario.Server.Backend.FailNext("submit-order", 1);
        var rolledBack = await operations.SubmitOrderAsync(
            new StressSubmitOrderRequest("SKU-ROLLBACK", 7, Guid.NewGuid().ToString("N")),
            new StressInventoryOptimisticUpdate(state, writer, 7),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        Record(
            "R03-rollback",
            "mutation 失败后乐观 State 精确回滚",
            !rolledBack.Succeeded
                && state.Get(PhaseD.StateCatalog.RemoteInventory).Value == beforeRollback
                && state.Get(PhaseD.StateCatalog.RemotePendingOptimistic).Value == 0,
            $"status={rolledBack.Status} before={beforeRollback} after={state.Get(PhaseD.StateCatalog.RemoteInventory).Value}");

        var quantityBeforeSerial = scenario.Server.Backend.Quantity;
        var serialTasks = Enumerable.Range(0, Math.Min(options.Workers, 32))
            .Select(index => operations.SubmitOrderAsync(
                new StressSubmitOrderRequest("SKU-SERIAL", 1, $"serial-{index}-{Guid.NewGuid():N}"),
                cancellationToken: cancellationToken).AsTask())
            .ToArray();
        var serialResults = await Task.WhenAll(serialTasks).ConfigureAwait(false);
        Record(
            "R04-keyed-serial",
            "同资源 mutation 经 KeyedSerial 全部完成且无库存丢失更新",
            serialResults.All(result => result.Succeeded)
                && scenario.Server.Backend.Quantity == quantityBeforeSerial - serialResults.Length,
            $"count={serialResults.Length} delta={quantityBeforeSerial - scenario.Server.Backend.Quantity}");

        var circuit = new DataResilienceOptions
        {
            PolicyName = $"stress-circuit-{Guid.NewGuid():N}",
            Scope = DataResiliencePolicyScope.Operation,
            CircuitBreaker = new DataCircuitBreakerOptions
            {
                IsEnabled = true,
                FailureThreshold = 2,
                BreakDuration = TimeSpan.FromSeconds(5),
            },
        };
        var callsBeforeCircuit = scenario.Server.Backend.CountOf("get-product");
        scenario.Server.Backend.FailNext("get-product", 2);
        var circuitFirst = await operations.GetProductAsync("SKU-CIRCUIT-1", resilience: circuit, cancellationToken: cancellationToken).ConfigureAwait(false);
        var circuitSecond = await operations.GetProductAsync("SKU-CIRCUIT-2", resilience: circuit, cancellationToken: cancellationToken).ConfigureAwait(false);
        var circuitThird = await operations.GetProductAsync("SKU-CIRCUIT-3", resilience: circuit, cancellationToken: cancellationToken).ConfigureAwait(false);
        var circuitTransportCalls = scenario.Server.Backend.CountOf("get-product") - callsBeforeCircuit;
        Record(
            "R05-circuit",
            "连续故障打开共享 circuit 并在 transport 前快速拒绝",
            !circuitFirst.Succeeded && !circuitSecond.Succeeded && !circuitThird.Succeeded
                && circuitTransportCalls == 2
                && circuitThird.Error?.Kind == DataErrorKind.ServiceUnavailable,
            $"calls={circuitTransportCalls} third={circuitThird.Error?.Kind}");

        var contributionRegistry = services.GetRequiredService<DataContributionRegistry>();
        var pipeline = services.GetRequiredService<IDataRequestPipeline>();
        var contributionResult = contributionRegistry.BeginContribution(
            "stress-plugin",
            $"stress-plugin-data-{Guid.NewGuid():N}",
            DataCapability.UseDataClient | DataCapability.UseHttpClient);
        var contribution = contributionResult.Value!;
        var contributionHandler = new CountingContributionHandler();
        contribution.RegisterHandler(contributionHandler);
        var pluginRequest = CreatePluginRequest(contribution);
        var delayBeforePlugin = scenario.Server.Backend.CountOf("delay");
        var pluginFirst = await pipeline.SendAsync(pluginRequest, cancellationToken).ConfigureAwait(false);
        var pluginCached = await pipeline.SendAsync(pluginRequest, cancellationToken).ConfigureAwait(false);
        await contribution.RevokeAsync().ConfigureAwait(false);
        var pluginStale = await pipeline.SendAsync(pluginRequest, cancellationToken).ConfigureAwait(false);
        Record(
            "R06-contribution",
            "plugin contribution 撤销 handler、缓存与 origin 权限",
            contributionResult.Succeeded && pluginFirst.Succeeded && pluginCached.Succeeded
                && pluginStale.Error?.Kind == DataErrorKind.PluginUnavailable
                && contributionHandler.InvocationCount == 1
                && scenario.Server.Backend.CountOf("delay") - delayBeforePlugin == 1,
            $"handler={contributionHandler.InvocationCount} transport={scenario.Server.Backend.CountOf("delay") - delayBeforePlugin} stale={pluginStale.Error?.Kind}");

        var delayed = operations.DelayAsync(5_000, cancellationToken: cancellationToken).AsTask();
        await Task.Delay(30, cancellationToken).ConfigureAwait(false);
        var stopA = scenario.Host.StopAsync(cancellationToken);
        var stopB = scenario.Host.StopAsync(cancellationToken);
        await Task.WhenAll(stopA, stopB).ConfigureAwait(false);
        var inFlight = await delayed.ConfigureAwait(false);
        var afterStop = await operations.GetProductAsync("SKU-AFTER-STOP", cancellationToken: cancellationToken).ConfigureAwait(false);
        Record(
            "R07-shutdown",
            "并发 Host Stop 共享事务、取消并 drain 在途请求、拒绝新请求",
            inFlight.Status == DataResultStatus.Cancelled
                && !afterStop.Succeeded
                && afterStop.Error?.Kind == DataErrorKind.PolicyRejected,
            $"inFlight={inFlight.Status} postStop={afterStop.Error?.Kind}");

        var probe = services.GetRequiredService<IStressDataRequestProbe>();
        var diagnostics = services.GetRequiredService<IDataDiagnostics>();
        var diagnosticText = string.Join('|', diagnostics.Records.Select(record => record.Message));
        Record(
            "R08-observability",
            "高并发下 operationId 唯一、诊断有界且不泄漏 token",
            probe.OperationIds.Count == probe.InvocationCount
                && diagnostics.Records.Count <= InMemoryDataDiagnostics.DefaultCapacity
                && !diagnosticText.Contains("stress/", StringComparison.Ordinal),
            $"handlers={probe.InvocationCount} ids={probe.OperationIds.Count} diagnostics={diagnostics.Records.Count}");
    }

    private static HttpDataRequest<string> CreatePluginRequest(DataContributionLease contribution) => new(
        "stress-plugin-client",
        "plugin-delay",
        StressRemoteOperations.ClientName,
        static _ => new HttpRequestMessage(HttpMethod.Get, "/api/delay/1"),
        static async (response, token) => await response.Content.ReadAsStringAsync(token).ConfigureAwait(false))
    {
        Origin = contribution.Origin,
        Cache = DataCacheOptions.Enabled(
            "plugin-delay",
            pluginContributionId: contribution.ContributionId,
            timeToLive: TimeSpan.FromMinutes(1)),
    };

    private sealed class CountingContributionHandler : IDataRequestHandler
    {
        private int _invocationCount;

        public int Order => 0;

        public int InvocationCount => Volatile.Read(ref _invocationCount);

        public async ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            DataRequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _invocationCount);
            return await next(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void Record(string id, string description, bool passed, string? detail = null) =>
        FixtureState.Report.Record(id, description, passed, detail);
}
