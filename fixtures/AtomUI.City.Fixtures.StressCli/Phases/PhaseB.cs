using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.DataIntegration;
using AtomUI.City.Fixtures.StressCli.Services;
using AtomUI.City.Fixtures.StressCli.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>Phase B：67 个业务服务的解析、生命周期、跨域接线和行为验证。</summary>
public static class PhaseB
{
    private static readonly Type[] SingletonServices =
    [
        typeof(IClockService), typeof(ISequenceService), typeof(IEnvironmentProbe), typeof(ICheckpointService),
        typeof(ITelemetrySink), typeof(IMetricsCollector), typeof(ISettingsStore), typeof(IDataCatalog),
        typeof(ICatalogIndex), typeof(ICatalogValidator), typeof(IMessageCodec), typeof(IMessageQueue),
        typeof(ISecurityBoundary), typeof(IScheduler), typeof(IOrderPolicy), typeof(IStockPolicy),
        typeof(ICustomerDirectory), typeof(IBillingCalculator), typeof(IAnalyticsEngine), typeof(INotificationDispatcher),
        typeof(IAuditTrail), typeof(IDashboardAggregator), typeof(IStoreFrontFacade), typeof(IShellNavigator),
        typeof(IIdentityDirectory), typeof(ITenantDirectory), typeof(IProductCatalog), typeof(IPriceBook),
        typeof(IShipmentTracker), typeof(IPaymentGateway), typeof(IReturnPolicy), typeof(ISearchIndex),
        typeof(ISupportDesk), typeof(IWorkflowEngine), typeof(INavigationAudit), typeof(IOperationsFacade),
        typeof(IStressAccessTokenSession), typeof(IStressDataRequestProbe), typeof(IStressRemoteOperations),
        typeof(IStressRemoteProjection), typeof(IStressDataConnectionFactory),
    ];

    private static readonly Type[] ScopedServices =
    [
        typeof(IMessageJournal), typeof(IMessageDeduper), typeof(IScheduleStore), typeof(IOrderValidator),
        typeof(IBillingLedger), typeof(IReportBuilder), typeof(IWorkspaceStore), typeof(IProductReader),
        typeof(IFulfillmentPlanner), typeof(IPickListStore), typeof(IPaymentLedger), typeof(IReturnCaseStore),
        typeof(ISearchSession), typeof(ISupportSession), typeof(IWorkflowRun),
    ];

    private static readonly Type[] TransientServices =
    [
        typeof(IOrderPricing), typeof(ITaxPolicy), typeof(IReportFormatter), typeof(ITrendDetector),
        typeof(IFlakyProbe), typeof(ITokenIssuer), typeof(IPromotionEngine), typeof(IShippingQuote),
        typeof(IRecommendationEngine), typeof(IFraudScorer),
        typeof(RemoteOperationsViewModel),
    ];

    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken);

        var provider = host.Services;
        Record("I03-resolve", "67 个业务服务全部可解析", () => ResolveAll(provider));
        Record("I03-singleton", "41 个 Singleton 两次解析为同一实例", () => CheckSingletons(provider));
        Record("I03-scoped", "15 个 Scoped 在 scope 内相同、跨 scope 不同", () => CheckScoped(provider));
        Record("I03-transient", "11 个 Transient 每次解析为新实例", () => CheckTransients(provider));
        Record("I03-cross-domain", "高层服务获得正确的跨模块依赖", () => CheckCrossDomain(provider));
        Record("I03-behavior", "服务联合业务行为产生确定结果", () => CheckBehavior(provider));

        await host.StopAsync(cancellationToken);
    }

    private static void ResolveAll(IServiceProvider provider)
    {
        if (SingletonServices.Length + ScopedServices.Length + TransientServices.Length != StressCliProgram.ServiceCount)
        {
            throw new InvalidOperationException("Service catalog count does not match the fixture contract.");
        }

        foreach (var type in SingletonServices.Concat(TransientServices))
        {
            _ = provider.GetRequiredService(type);
        }

        using var scope = provider.CreateScope();
        foreach (var type in ScopedServices)
        {
            _ = scope.ServiceProvider.GetRequiredService(type);
        }
    }

    private static void CheckSingletons(IServiceProvider provider)
    {
        foreach (var type in SingletonServices)
        {
            if (!ReferenceEquals(provider.GetRequiredService(type), provider.GetRequiredService(type)))
            {
                throw new InvalidOperationException($"Singleton {type.Name} returned different instances.");
            }
        }
    }

    private static void CheckScoped(IServiceProvider provider)
    {
        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        foreach (var type in ScopedServices)
        {
            var first = scopeA.ServiceProvider.GetRequiredService(type);
            var second = scopeA.ServiceProvider.GetRequiredService(type);
            var foreign = scopeB.ServiceProvider.GetRequiredService(type);
            if (!ReferenceEquals(first, second) || ReferenceEquals(first, foreign))
            {
                throw new InvalidOperationException($"Scoped lifetime contract failed for {type.Name}.");
            }
        }
    }

    private static void CheckTransients(IServiceProvider provider)
    {
        foreach (var type in TransientServices)
        {
            if (ReferenceEquals(provider.GetRequiredService(type), provider.GetRequiredService(type)))
            {
                throw new InvalidOperationException($"Transient {type.Name} returned the same instance twice.");
            }
        }
    }

    private static void CheckCrossDomain(IServiceProvider provider)
    {
        var engine = provider.GetRequiredService<IAnalyticsEngine>();
        var collector = provider.GetRequiredService<IMetricsCollector>();
        if (!ReferenceEquals(engine.Collector, collector))
        {
            throw new InvalidOperationException("Analytics did not receive the shared metrics collector.");
        }

        var dashboard = provider.GetRequiredService<IDashboardAggregator>();
        if (!ReferenceEquals(dashboard.Engine, engine))
        {
            throw new InvalidOperationException("Dashboard did not receive the shared analytics engine.");
        }

        var storefront = provider.GetRequiredService<IStoreFrontFacade>();
        if (!ReferenceEquals(storefront.Directory, provider.GetRequiredService<ICustomerDirectory>()) ||
            !ReferenceEquals(storefront.Stock, provider.GetRequiredService<IStockPolicy>()))
        {
            throw new InvalidOperationException("StoreFront cross-domain dependencies are not shared.");
        }
    }

    private static void CheckBehavior(IServiceProvider provider)
    {
        var settings = provider.GetRequiredService<ISettingsStore>();
        settings.Set("pricing.multiplier", "1.25");

        var products = provider.GetRequiredService<IProductCatalog>();
        products.Upsert("SKU-0001", 20m);
        products.Upsert("SKU-0002", 30m);
        provider.GetRequiredService<ISearchIndex>().Index("SKU-0001");
        provider.GetRequiredService<ISearchIndex>().Index("SKU-0002");

        var identity = provider.GetRequiredService<IIdentityDirectory>();
        var session = identity.SignIn("operator-a");
        var token = provider.GetRequiredService<ITokenIssuer>().Issue(session);
        var tenant = provider.GetRequiredService<ITenantDirectory>().Switch("tenant-east");
        var receipt = provider.GetRequiredService<IOperationsFacade>().Execute("operator-a", "SKU-0001", 2);

        using var scope = provider.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<IFulfillmentPlanner>();
        var plan = planner.Plan("SKU-0001", 2);
        var picks = scope.ServiceProvider.GetRequiredService<IPickListStore>();
        picks.Add(plan);
        var ledger = scope.ServiceProvider.GetRequiredService<IPaymentLedger>();
        ledger.Capture(receipt.Amount);
        var search = scope.ServiceProvider.GetRequiredService<ISearchSession>();
        var results = search.Execute(provider.GetRequiredService<ISearchIndex>(), "0001");

        if (!token.StartsWith("token:session:operator-a", StringComparison.Ordinal) ||
            tenant != "tenant-east" || products.Count != 2 || receipt.Amount != 50m ||
            picks.Count != 1 || ledger.Total != 50m || results.Count != 1)
        {
            throw new InvalidOperationException(
                $"Unexpected service result: tenant={tenant} products={products.Count} amount={receipt.Amount} picks={picks.Count} ledger={ledger.Total} search={results.Count}.");
        }
    }

    private static void Record(string id, string description, Action action)
    {
        try
        {
            action();
            FixtureState.Report.Record(id, description, true);
        }
        catch (Exception exception)
        {
            FixtureState.Report.Record(id, description, false, exception.Message);
        }
    }
}
