using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Services;

namespace AtomUI.City.Fixtures.StressCli.Modules;

// L0 —— 根模块（无依赖）

public sealed class FoundationModule : FixtureModule
{
    public override string FixtureName => "Foundation";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IClockService, SystemClockService>();
        context.Services.AddSingleton<ISequenceService, SequenceService>();
        context.Services.AddSingleton<IEnvironmentProbe, EnvironmentProbeService>();
        context.Services.AddSingleton<ICheckpointService, CheckpointService>();
    }
}

// Telemetry(2)

public sealed class TelemetryModule : FixtureModule
{
    public override string FixtureName => "Telemetry";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ITelemetrySink, InMemoryTelemetrySink>();
        context.Services.AddSingleton<IMetricsCollector, MetricsCollector>();
    }
}

public sealed class SettingsModule : FixtureModule
{
    public override string FixtureName => "Settings";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ISettingsStore, InMemorySettingsStore>();
    }
}

// L1 —— 一级领域模块

[DependsOn(typeof(FoundationModule))]
public sealed class DataCatalogModule : FixtureModule
{
    public override string FixtureName => "DataCatalog";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IDataCatalog, CatalogService>();
        context.Services.AddSingleton<ICatalogIndex, IndexService>();
        context.Services.AddSingleton<ICatalogValidator, CatalogValidator>();
    }
}

[DependsOn(typeof(FoundationModule))]
public sealed class MessagingModule : FixtureModule
{
    public override string FixtureName => "Messaging";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IMessageCodec, JsonMessageCodec>();
        context.Services.AddScoped<IMessageJournal, JournalService>();
        context.Services.AddScoped<IMessageDeduper, DeduperService>();
        context.Services.AddSingleton<IMessageQueue, MessageQueueService>();
    }
}

[DependsOn(typeof(FoundationModule))]
public sealed class SecurityModule : FixtureModule
{
    public override string FixtureName => "Security";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ISecurityBoundary, SecurityBoundaryService>();
    }
}

[DependsOn(typeof(FoundationModule))]
[DependsOn(typeof(TelemetryModule))]
public sealed class SchedulingModule : FixtureModule
{
    public override string FixtureName => "Scheduling";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IScheduler, SchedulerService>();
        context.Services.AddScoped<IScheduleStore, ScheduleStoreService>();
    }
}

[DependsOn(typeof(TelemetryModule))]
public sealed class TelemetryArchiveModule : FixtureModule
{
    public override string FixtureName => "TelemetryArchive";
}

// L2 —— 二级领域模块（含钻石入边）

[DependsOn(typeof(DataCatalogModule))]
public sealed class CatalogIndexModule : FixtureModule
{
    public override string FixtureName => "CatalogIndex";
}

[DependsOn(typeof(DataCatalogModule))]
[DependsOn(typeof(MessagingModule))]
public sealed class OrdersModule : FixtureModule
{
    public override string FixtureName => "Orders";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IOrderPolicy, OrderPolicyService>();
        context.Services.AddTransient<IOrderPricing, PricingService>();
        context.Services.AddScoped<IOrderValidator, OrderValidatorService>();
    }
}

[DependsOn(typeof(DataCatalogModule))]
public sealed class InventoryModule : FixtureModule
{
    public override string FixtureName => "Inventory";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IStockPolicy, StockPolicyService>();
    }
}

[DependsOn(typeof(DataCatalogModule))]
[DependsOn(typeof(MessagingModule))]
public sealed class CustomersModule : FixtureModule
{
    public override string FixtureName => "Customers";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ICustomerDirectory, CustomerDirectoryService>();
    }
}
