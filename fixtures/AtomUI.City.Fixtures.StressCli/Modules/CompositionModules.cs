using AtomUI.City.Core.Modularity;
using AtomUI.City.Fixtures.StressCli.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli.Modules;

// L3 —— 三级聚合模块

[DependsOn(typeof(OrdersModule))]
[DependsOn(typeof(SecurityModule))]
public sealed class BillingModule : FixtureModule
{
    public override string FixtureName => "Billing";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IBillingCalculator, BillingCalculatorService>();
        context.Services.AddScoped<IBillingLedger, BillingLedgerService>();
    }
}

[DependsOn(typeof(SchedulingModule))]
[DependsOn(typeof(TelemetryModule))]
public sealed class ReportingModule : FixtureModule
{
    public override string FixtureName => "Reporting";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<IReportBuilder, ReportBuilderService>();
        context.Services.AddTransient<IReportFormatter, ReportFormatterService>();
    }
}

[DependsOn(typeof(BillingModule))]
public sealed class BillingTaxModule : FixtureModule
{
    public override string FixtureName => "BillingTax";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<ITaxPolicy, TaxPolicyService>();
    }
}

// L4 —— 四级分析模块

[DependsOn(typeof(OrdersModule))]
[DependsOn(typeof(InventoryModule))]
[DependsOn(typeof(ReportingModule))]
public sealed class AnalyticsModule : FixtureModule
{
    public override string FixtureName => "Analytics";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IAnalyticsEngine, AnalyticsEngineService>();
        context.Services.AddTransient<ITrendDetector, TrendDetectorService>();
    }
}

[DependsOn(typeof(MessagingModule))]
[DependsOn(typeof(CustomersModule))]
public sealed class NotificationsModule : FixtureModule
{
    public override string FixtureName => "Notifications";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<INotificationDispatcher, NotificationDispatcherService>();
    }
}

[DependsOn(typeof(BillingModule))]
[DependsOn(typeof(ReportingModule))]
[DependsOn(typeof(SecurityModule))]
public sealed class AuditModule : FixtureModule
{
    public override string FixtureName => "Audit";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IAuditTrail, AuditTrailService>();
    }
}

// L5 —— 五级呈现模块

[DependsOn(typeof(AnalyticsModule))]
[DependsOn(typeof(NotificationsModule))]
public sealed class DashboardModule : FixtureModule
{
    public override string FixtureName => "Dashboard";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IDashboardAggregator, DashboardAggregatorService>();
    }
}

[DependsOn(typeof(DashboardModule))]
[DependsOn(typeof(BillingModule))]
[DependsOn(typeof(AuditModule))]
public sealed class WorkspaceModule : FixtureModule
{
    public override string FixtureName => "Workspace";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<IWorkspaceStore, WorkspaceStoreService>();
    }
}

// L6 —— 联合阵地

[DependsOn(typeof(WorkspaceModule))]
public sealed class ShellModule : FixtureModule
{
    public override string FixtureName => "Shell";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IShellNavigator, ShellNavigatorService>();
    }
}

[DependsOn(typeof(WorkspaceModule))]
public sealed class StoreFrontModule : FixtureModule
{
    public override string FixtureName => "StoreFront";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IStoreFrontFacade, StoreFrontFacadeService>();
    }
}
