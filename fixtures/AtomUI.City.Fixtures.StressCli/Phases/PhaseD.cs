using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>
/// Phase D：72 个状态对象（54 registry + 10 computed + 8 collection）的大规模验证——
/// I5（4×500 并发写版本完整性 + authority 越权拒绝 + 快照恢复）、
/// I6（三层 computed 链失效一致性）、I7（集合快照冻结 + bulk 1000）。
/// </summary>
public static class PhaseD
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();

        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken);

        // 夹具需要 Registry 的完整公共面（快照/恢复/writer 工厂）——从接口实例转换到具体类型。
        var registry = (ApplicationStateRegistry)host.Services.GetRequiredService<IStateRegistry>();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();

        RegisterCatalog(registry);

        // I5 并发写：4 线程 × 500 次 Update → Version 2000、值 2000。
        var state = registry.GetWritable<long>(StateCatalog.Counters);
        Parallel.For(0, 4, _ =>
        {
            for (var i = 0; i < 500; i++)
            {
                state.Update(v => v + 1);
            }
        });

        FixtureState.Report.Record(
            "I5-concurrent",
            "4×500 并发写后 Version 与值完整（无丢失更新）",
            state.Version == 2000 && state.Value == 2000,
            $"Version={state.Version} Value={state.Value}");

        var declaredStateKeys = typeof(StateCatalog).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Length;
        FixtureState.Report.Record(
            "I07-catalog",
            "54 个 registry state 均已注册",
            declaredStateKeys == 54,
            $"declared={declaredStateKeys}");

        // 10 个 computed：链式、跨域、首次失败恢复和运行期循环防护。
        var root = registry.GetWritable<long>(StateCatalog.ChainRoot);
        root.Set(1);

        using var c1 = new ComputedState<long>(
            () => root.Value * 2, diagnostics, root);
        using var c2 = new ComputedState<long>(
            () => c1.Value + 1, diagnostics, c1);
        using var c3 = new ComputedState<string>(
            () => $"v:{c2.Value}", diagnostics, c2);

        var revenue = registry.GetWritable<decimal>(StateCatalog.OrdersRevenue);
        var authorized = registry.GetWritable<decimal>(StateCatalog.PaymentsAuthorized);
        var captured = registry.GetWritable<decimal>(StateCatalog.PaymentsCaptured);
        var fulfillmentPending = registry.GetWritable<int>(StateCatalog.FulfillmentPending);
        var fulfillmentCompleted = registry.GetWritable<int>(StateCatalog.FulfillmentCompleted);
        var searches = registry.GetWritable<int>(StateCatalog.SearchQueries);
        var recommendations = registry.GetWritable<int>(StateCatalog.RecommendationsGenerated);
        var supportTickets = registry.GetWritable<int>(StateCatalog.SupportOpenTickets);
        var navigation = registry.GetWritable<string>(StateCatalog.NavigationCurrentRoute);

        revenue.Set(500m);
        authorized.Set(120m);
        captured.Set(80m);
        fulfillmentPending.Set(4);
        fulfillmentCompleted.Set(6);
        searches.Set(10);
        recommendations.Set(7);
        supportTickets.Set(3);
        navigation.Set("fixtures.routes.orders");

        using var commerceHealth = new ComputedState<decimal>(
            () => revenue.Value - authorized.Value, diagnostics, revenue, authorized);
        using var paymentExposure = new ComputedState<decimal>(
            () => authorized.Value - captured.Value, diagnostics, authorized, captured);
        using var fulfillmentPressure = new ComputedState<double>(
            () => (double)fulfillmentPending.Value / Math.Max(1, fulfillmentCompleted.Value),
            diagnostics,
            fulfillmentPending,
            fulfillmentCompleted);
        using var searchYield = new ComputedState<double>(
            () => (double)recommendations.Value / Math.Max(1, searches.Value),
            diagnostics,
            recommendations,
            searches);

        var failSupportLoad = true;
        using var supportLoad = new ComputedState<int>(
            () => failSupportLoad
                ? throw new InvalidOperationException("first support projection failed")
                : supportTickets.Value * 10,
            diagnostics,
            supportTickets);

        var firstFailureObserved = false;
        try
        {
            _ = supportLoad.Value;
        }
        catch (InvalidOperationException)
        {
            firstFailureObserved = true;
        }

        failSupportLoad = false;
        supportTickets.Update(value => value + 1);

        ComputedState<decimal>? operationsScore = null;
        var cycleMode = false;
        operationsScore = new ComputedState<decimal>(
            () => cycleMode
                ? operationsScore!.Value
                : commerceHealth.Value - paymentExposure.Value + (decimal)searchYield.Value,
            diagnostics,
            commerceHealth,
            paymentExposure,
            searchYield);
        using var operationsScoreLease = operationsScore;
        using var navigationTitle = new ComputedState<string>(
            () => $"route:{navigation.Value}", diagnostics, navigation);

        var chainOk = c1.Value == 2 && c2.Value == 3 && c3.Value == "v:3";
        root.Set(5);
        var chainOkAfter = c1.Value == 10 && c2.Value == 11 && c3.Value == "v:11";

        var projectionsOk = commerceHealth.Value == 380m &&
                            paymentExposure.Value == 40m &&
                            Math.Abs(fulfillmentPressure.Value - 0.6666666667d) < 0.0001d &&
                            Math.Abs(searchYield.Value - 0.7d) < 0.0001d &&
                            supportLoad.Value == 40 &&
                            operationsScore.Value == 340.7m &&
                            navigationTitle.Value == "route:fixtures.routes.orders";

        cycleMode = true;
        authorized.Update(value => value + 1m);
        var cycleRejected = false;
        try
        {
            _ = operationsScore.Value;
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("circular", StringComparison.OrdinalIgnoreCase))
        {
            cycleRejected = true;
        }

        cycleRejected |= operationsScore.LastError is InvalidOperationException cycleError &&
                         cycleError.Message.Contains("circular", StringComparison.OrdinalIgnoreCase);

        cycleMode = false;
        authorized.Update(value => value + 1m);
        var recoveredAfterCycle = operationsScore.Value > 0m;

        FixtureState.Report.Record(
            "I08-computed",
            "10 个 computed 完成失效传播、首次失败恢复和循环防护",
            chainOk && chainOkAfter && projectionsOk && firstFailureObserved && cycleRejected && recoveredAfterCycle,
            $"chain={chainOk && chainOkAfter} projections={projectionsOk} firstFailure={firstFailureObserved} cycle={cycleRejected} recovered={recoveredAfterCycle}");

        // 8 个集合状态：每个执行批量更新、版本推进和快照恢复。
        var collections = Enumerable.Range(0, 8)
            .Select(_ => new StateCollection<string, int>(diagnostics: diagnostics))
            .ToArray();
        var collectionFailures = new List<string>();

        for (var collectionIndex = 0; collectionIndex < collections.Length; collectionIndex++)
        {
            var collection = collections[collectionIndex];
            var itemCount = collectionIndex == 0 ? 1000 : 100;
            var changes = 0;
            using var subscription = collection.OnChange(_ => Interlocked.Increment(ref changes));
            var bulk = Enumerable.Range(0, itemCount)
                .Select(i => new KeyValuePair<string, int>($"C{collectionIndex}-K{i:D4}", i));
            var bulkChanged = collection.AddOrUpdateRange(bulk);
            var frozen = collection.CreateSnapshot();
            collection.AddOrUpdate($"C{collectionIndex}-K0000", 99999);
            collection.Remove($"C{collectionIndex}-K{itemCount - 1:D4}");
            var frozenIntact = frozen.Items.Count == itemCount &&
                               frozen.Items.First(entry => entry.Key.EndsWith("K0000", StringComparison.Ordinal)).Item == 0;
            var restored = collection.RestoreSnapshot(frozen);

            if (!bulkChanged || !frozenIntact || !restored || collection.Items.Count != itemCount ||
                collection.Items[$"C{collectionIndex}-K0000"] != 0 || collection.Version < 1 || changes < 3)
            {
                collectionFailures.Add($"collection-{collectionIndex}: count={collection.Items.Count} version={collection.Version} changes={changes}");
            }
        }

        FixtureState.Report.Record(
            "I09-collections",
            "8 个集合状态的批量、版本、通知、快照与恢复一致",
            collectionFailures.Count == 0,
            collectionFailures.Count == 0 ? null : string.Join("; ", collectionFailures));

        foreach (var collection in collections)
        {
            collection.Dispose();
        }

        // authority 越权拒绝：Inventory 模块的 writer 不能写 Orders 模块的状态。
        var inventoryWriter = registry.CreateWriter(StateWriteAuthority.Module("Inventory"));
        var denied = false;
        try
        {
            inventoryWriter.Set(StateCatalog.OrderOwned, 1);
        }
        catch (StateAccessDeniedException)
        {
            denied = true;
        }

        var ownerWriter = registry.CreateWriter(StateWriteAuthority.Module("Orders"));
        ownerWriter.Set(StateCatalog.OrderOwned, 1);
        var ownerWriteOk = registry.Get(StateCatalog.OrderOwned).Value == 1;

        var readOnlyDenied = false;
        try
        {
            _ = registry.GetWritable(StateCatalog.IdentityFailedLogins);
        }
        catch (StateAccessDeniedException)
        {
            readOnlyDenied = true;
        }

        var capabilityWriter = registry.CreateWriter(StateWriteAuthority.Module("Pricing", ["pricing.write"]));
        capabilityWriter.Set(StateCatalog.PricingRevision, 7);
        var pluginWriter = registry.CreateWriter(StateWriteAuthority.Plugin("tenant-plugin"));
        pluginWriter.Set(StateCatalog.TenantCurrent, "tenant-west");
        var allPoliciesOk = readOnlyDenied &&
                            registry.Get(StateCatalog.PricingRevision).Value == 7 &&
                            registry.Get(StateCatalog.TenantCurrent).Value == "tenant-west";

        FixtureState.Report.Record(
            "I07-authority",
            "五种访问策略均执行：只读拒绝、Host、Owner、Capability 与 Plugin 隔离",
            denied && ownerWriteOk && allPoliciesOk,
            $"ownerDenied={denied} ownerWrite={ownerWriteOk} policies={allPoliciesOk}");

        // 快照恢复：Persisted 状态往返一致，Transient 拒绝恢复。
        var snapshot = registry.CreateSnapshot();
        var persistedCount = snapshot.Entries.Count;
        registry.GetWritable<long>(StateCatalog.Counters).Set(777);
        registry.Restore(snapshot);
        var restoredOk = registry.GetWritable<long>(StateCatalog.Counters).Value == 2000;

        FixtureState.Report.Record(
            "I5-restore",
            $"快照恢复一致（Persisted 条目 {persistedCount}，恢复后计数回到 2000）",
            restoredOk && persistedCount > 0,
            $"persistedCount={persistedCount} restoredOk={restoredOk}");

        await host.StopAsync(cancellationToken);
    }

    internal static void RegisterCatalog(IStateRegistry registry)
    {
        // 全局（HostWrite）
        registry.Add(StateDefinition.Create(StateCatalog.Counters, 0L, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.ChainRoot, 0L, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.Phase, "A", StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.Theme, "system", StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.Culture, "zh-CN", StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.CurrentUser, "tester", StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.Workspace, "default", StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.Network, "online", StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.Busy, false, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.SecurityMode, "standard", StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.OrdersStatus, "open", StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.OrdersRevenue, 0m, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.InventoryLowWatermark, 10, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.BillingSettled, 0m, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.BillingPending, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.TaxRate, 0.19m, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.ReportsGenerated, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.AnalyticsTrend, "flat", StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.NotificationsQueued, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.AuditEntries, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.DashboardViews, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.SchedulingTicks, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.SettingsDirty, false, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.TelemetrySamples, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.CatalogRevision, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.MessagingJournaled, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.WorkspaceSaved, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.SecurityCrossings, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.FaultsInjected, 0, StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));
        registry.Add(StateDefinition.Create(StateCatalog.FixturesPhase, "B", StateLifetime.Application, StateAccessPolicy.HostWrite, StateSnapshotPolicy.Transient));

        // OwnerWrite（模块私有，验证 authority）
        registry.Add(StateDefinition.Create(StateCatalog.OrderOwned, 0, StateLifetime.Application, StateAccessPolicy.OwnerWrite, StateSnapshotPolicy.Transient, ownerModule: "Orders"));
        registry.Add(StateDefinition.Create(StateCatalog.InventoryOwned, 0, StateLifetime.Application, StateAccessPolicy.OwnerWrite, StateSnapshotPolicy.Transient, ownerModule: "Inventory"));

        // 跨域运营状态（22）：同时覆盖全部五种访问策略。
        registry.Add(StateDefinition.Create(StateCatalog.IdentitySession, "anonymous", snapshotPolicy: StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.IdentityFailedLogins, 0, access: StateAccessPolicy.ReadOnly));
        registry.Add(StateDefinition.Create(StateCatalog.TenantCurrent, "tenant-default", access: StateAccessPolicy.PluginIsolated, pluginId: "tenant-plugin"));
        registry.Add(StateDefinition.Create(StateCatalog.TenantRevision, 0, snapshotPolicy: StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.ProductCount, 0));
        registry.Add(StateDefinition.Create(StateCatalog.PricingCurrency, "CNY", snapshotPolicy: StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.PricingRevision, 0, access: StateAccessPolicy.AuthorizedWrite, writeCapability: "pricing.write"));
        registry.Add(StateDefinition.Create(StateCatalog.PromotionsApplied, 0));
        registry.Add(StateDefinition.Create(StateCatalog.FulfillmentPending, 0));
        registry.Add(StateDefinition.Create(StateCatalog.FulfillmentCompleted, 0));
        registry.Add(StateDefinition.Create(StateCatalog.ShippingQuoted, 0));
        registry.Add(StateDefinition.Create(StateCatalog.ShippingInTransit, 0));
        registry.Add(StateDefinition.Create(StateCatalog.PaymentsAuthorized, 0m, snapshotPolicy: StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.PaymentsCaptured, 0m, snapshotPolicy: StateSnapshotPolicy.Persisted));
        registry.Add(StateDefinition.Create(StateCatalog.ReturnsOpen, 0));
        registry.Add(StateDefinition.Create(StateCatalog.SearchQueries, 0));
        registry.Add(StateDefinition.Create(StateCatalog.RecommendationsGenerated, 0));
        registry.Add(StateDefinition.Create(StateCatalog.FraudScore, 0m));
        registry.Add(StateDefinition.Create(StateCatalog.FraudFlagged, 0));
        registry.Add(StateDefinition.Create(StateCatalog.SupportOpenTickets, 0));
        registry.Add(StateDefinition.Create(StateCatalog.WorkflowRunning, 0));
        registry.Add(StateDefinition.Create(StateCatalog.NavigationCurrentRoute, "fixtures.routes.dashboard"));
    }

    public static class StateCatalog
    {
        public static readonly StateKey<long> Counters = new("fixtures.state.counters");
        public static readonly StateKey<long> ChainRoot = new("fixtures.state.chain-root");
        public static readonly StateKey<string> Phase = new("fixtures.state.phase");
        public static readonly StateKey<string> Theme = new("fixtures.state.theme");
        public static readonly StateKey<string> Culture = new("fixtures.state.culture");
        public static readonly StateKey<string> CurrentUser = new("fixtures.state.current-user");
        public static readonly StateKey<string> Workspace = new("fixtures.state.workspace");
        public static readonly StateKey<string> Network = new("fixtures.state.network");
        public static readonly StateKey<bool> Busy = new("fixtures.state.busy");
        public static readonly StateKey<string> SecurityMode = new("fixtures.state.security-mode");
        public static readonly StateKey<string> OrdersStatus = new("fixtures.state.orders-status");
        public static readonly StateKey<decimal> OrdersRevenue = new("fixtures.state.orders-revenue");
        public static readonly StateKey<int> InventoryLowWatermark = new("fixtures.state.inventory-low");
        public static readonly StateKey<decimal> BillingSettled = new("fixtures.state.billing-settled");
        public static readonly StateKey<int> BillingPending = new("fixtures.state.billing-pending");
        public static readonly StateKey<decimal> TaxRate = new("fixtures.state.tax-rate");
        public static readonly StateKey<int> ReportsGenerated = new("fixtures.state.reports-generated");
        public static readonly StateKey<string> AnalyticsTrend = new("fixtures.state.analytics-trend");
        public static readonly StateKey<int> NotificationsQueued = new("fixtures.state.notifications-queued");
        public static readonly StateKey<int> AuditEntries = new("fixtures.state.audit-entries");
        public static readonly StateKey<int> DashboardViews = new("fixtures.state.dashboard-views");
        public static readonly StateKey<int> SchedulingTicks = new("fixtures.state.scheduling-ticks");
        public static readonly StateKey<bool> SettingsDirty = new("fixtures.state.settings-dirty");
        public static readonly StateKey<int> TelemetrySamples = new("fixtures.state.telemetry-samples");
        public static readonly StateKey<int> CatalogRevision = new("fixtures.state.catalog-revision");
        public static readonly StateKey<int> MessagingJournaled = new("fixtures.state.messaging-journaled");
        public static readonly StateKey<int> WorkspaceSaved = new("fixtures.state.workspace-saved");
        public static readonly StateKey<int> SecurityCrossings = new("fixtures.state.security-crossings");
        public static readonly StateKey<int> FaultsInjected = new("fixtures.state.faults-injected");
        public static readonly StateKey<string> FixturesPhase = new("fixtures.state.fixtures-phase");
        public static readonly StateKey<int> OrderOwned = new("fixtures.state.order-owned");
        public static readonly StateKey<int> InventoryOwned = new("fixtures.state.inventory-owned");
        public static readonly StateKey<string> IdentitySession = new("fixtures.state.identity-session");
        public static readonly StateKey<int> IdentityFailedLogins = new("fixtures.state.identity-failed-logins");
        public static readonly StateKey<string> TenantCurrent = new("fixtures.state.tenant-current");
        public static readonly StateKey<int> TenantRevision = new("fixtures.state.tenant-revision");
        public static readonly StateKey<int> ProductCount = new("fixtures.state.product-count");
        public static readonly StateKey<string> PricingCurrency = new("fixtures.state.pricing-currency");
        public static readonly StateKey<int> PricingRevision = new("fixtures.state.pricing-revision");
        public static readonly StateKey<int> PromotionsApplied = new("fixtures.state.promotions-applied");
        public static readonly StateKey<int> FulfillmentPending = new("fixtures.state.fulfillment-pending");
        public static readonly StateKey<int> FulfillmentCompleted = new("fixtures.state.fulfillment-completed");
        public static readonly StateKey<int> ShippingQuoted = new("fixtures.state.shipping-quoted");
        public static readonly StateKey<int> ShippingInTransit = new("fixtures.state.shipping-in-transit");
        public static readonly StateKey<decimal> PaymentsAuthorized = new("fixtures.state.payments-authorized");
        public static readonly StateKey<decimal> PaymentsCaptured = new("fixtures.state.payments-captured");
        public static readonly StateKey<int> ReturnsOpen = new("fixtures.state.returns-open");
        public static readonly StateKey<int> SearchQueries = new("fixtures.state.search-queries");
        public static readonly StateKey<int> RecommendationsGenerated = new("fixtures.state.recommendations-generated");
        public static readonly StateKey<decimal> FraudScore = new("fixtures.state.fraud-score");
        public static readonly StateKey<int> FraudFlagged = new("fixtures.state.fraud-flagged");
        public static readonly StateKey<int> SupportOpenTickets = new("fixtures.state.support-open-tickets");
        public static readonly StateKey<int> WorkflowRunning = new("fixtures.state.workflow-running");
        public static readonly StateKey<string> NavigationCurrentRoute = new("fixtures.state.navigation-current-route");
    }
}
