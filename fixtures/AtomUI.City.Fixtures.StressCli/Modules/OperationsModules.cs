using AtomUI.City.Core.Modularity;
using AtomUI.City.Data;
using AtomUI.City.Fixtures.StressCli.DataIntegration;
using AtomUI.City.Fixtures.StressCli.Services;
using AtomUI.City.Fixtures.StressCli.Routing;
using AtomUI.City.Fixtures.StressCli.ViewModels;
using AtomUI.City.Security;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli.Modules;

[DependsOn(typeof(FoundationModule))]
[DependsOn(typeof(SecurityModule))]
public sealed class IdentityModule : FixtureModule
{
    public override string FixtureName => "Identity";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IIdentityDirectory, IdentityDirectoryService>();
        context.Services.AddTransient<ITokenIssuer, TokenIssuerService>();
    }
}

[DependsOn(typeof(FoundationModule))]
[DependsOn(typeof(SettingsModule))]
public sealed class TenancyModule : FixtureModule
{
    public override string FixtureName => "Tenancy";

    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddSingleton<ITenantDirectory, TenantDirectoryService>();
}

[DependsOn(typeof(DataCatalogModule))]
[DependsOn(typeof(CatalogIndexModule))]
public sealed class ProductModule : FixtureModule
{
    public override string FixtureName => "Product";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IProductCatalog, ProductCatalogService>();
        context.Services.AddScoped<IProductReader, ProductReaderService>();
    }
}

[DependsOn(typeof(ProductModule))]
[DependsOn(typeof(SettingsModule))]
public sealed class PricingModule : FixtureModule
{
    public override string FixtureName => "Pricing";

    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddSingleton<IPriceBook, PriceBookService>();
}

[DependsOn(typeof(PricingModule))]
[DependsOn(typeof(CustomersModule))]
public sealed class PromotionsModule : FixtureModule
{
    public override string FixtureName => "Promotions";

    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddTransient<IPromotionEngine, PromotionEngineService>();
}

[DependsOn(typeof(OrdersModule))]
[DependsOn(typeof(InventoryModule))]
[DependsOn(typeof(ProductModule))]
public sealed class FulfillmentModule : FixtureModule
{
    public override string FixtureName => "Fulfillment";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<IFulfillmentPlanner, FulfillmentPlannerService>();
        context.Services.AddScoped<IPickListStore, PickListStoreService>();
    }
}

[DependsOn(typeof(FulfillmentModule))]
[DependsOn(typeof(SchedulingModule))]
public sealed class ShippingModule : FixtureModule
{
    public override string FixtureName => "Shipping";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<IShippingQuote, ShippingQuoteService>();
        context.Services.AddSingleton<IShipmentTracker, ShipmentTrackerService>();
    }
}

[DependsOn(typeof(OrdersModule))]
[DependsOn(typeof(SecurityModule))]
[DependsOn(typeof(TelemetryModule))]
public sealed class FraudModule : FixtureModule
{
    public override string FixtureName => "Fraud";

    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddTransient<IFraudScorer, FraudScorerService>();
}

[DependsOn(typeof(BillingModule))]
[DependsOn(typeof(SecurityModule))]
[DependsOn(typeof(FraudModule))]
public sealed class PaymentsModule : FixtureModule
{
    public override string FixtureName => "Payments";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IPaymentGateway, PaymentGatewayService>();
        context.Services.AddScoped<IPaymentLedger, PaymentLedgerService>();
    }
}

[DependsOn(typeof(OrdersModule))]
[DependsOn(typeof(InventoryModule))]
[DependsOn(typeof(PaymentsModule))]
public sealed class ReturnsModule : FixtureModule
{
    public override string FixtureName => "Returns";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IReturnPolicy, ReturnPolicyService>();
        context.Services.AddScoped<IReturnCaseStore, ReturnCaseStoreService>();
    }
}

[DependsOn(typeof(ProductModule))]
[DependsOn(typeof(TelemetryModule))]
public sealed class SearchModule : FixtureModule
{
    public override string FixtureName => "Search";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ISearchIndex, SearchIndexService>();
        context.Services.AddScoped<ISearchSession, SearchSessionService>();
    }
}

[DependsOn(typeof(SearchModule))]
[DependsOn(typeof(AnalyticsModule))]
[DependsOn(typeof(CustomersModule))]
public sealed class RecommendationsModule : FixtureModule
{
    public override string FixtureName => "Recommendations";

    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddTransient<IRecommendationEngine, RecommendationEngineService>();
}

[DependsOn(typeof(AuditModule))]
[DependsOn(typeof(CustomersModule))]
[DependsOn(typeof(MessagingModule))]
public sealed class SupportModule : FixtureModule
{
    public override string FixtureName => "Support";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ISupportDesk, SupportDeskService>();
        context.Services.AddScoped<ISupportSession, SupportSessionService>();
    }
}

[DependsOn(typeof(AuditModule))]
[DependsOn(typeof(SchedulingModule))]
[DependsOn(typeof(MessagingModule))]
public sealed class WorkflowModule : FixtureModule
{
    public override string FixtureName => "Workflow";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IWorkflowEngine, WorkflowEngineService>();
        context.Services.AddScoped<IWorkflowRun, WorkflowRunService>();
    }
}

[DependsOn(typeof(ShellModule))]
[DependsOn(typeof(WorkspaceModule))]
[DependsOn(typeof(DashboardModule))]
public sealed class NavigationModule : FixtureModule
{
    public override string FixtureName => "Navigation";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<INavigationAudit, NavigationAuditService>();
        context.Services.AddTransient<FixtureRouteMatchPolicy>();
        context.Services.AddTransient<FixtureRouteGuard>();
        context.Services.AddTransient<FixtureRouteResolver>();
        context.Services.AddTransient<FixtureRouteMiddleware>();
    }
}

[DependsOn(typeof(NavigationModule))]
[DependsOn(typeof(PaymentsModule))]
[DependsOn(typeof(ReturnsModule))]
[DependsOn(typeof(RecommendationsModule))]
[DependsOn(typeof(SupportModule))]
[DependsOn(typeof(WorkflowModule))]
[DependsOn(typeof(DataModule))]
public sealed class OperationsModule : FixtureModule
{
    public override string FixtureName => "Operations";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IOperationsFacade, OperationsFacadeService>();
        context.Services.AddSingleton<StressAccessTokenSession>();
        context.Services.AddSingleton<IStressAccessTokenSession>(provider =>
            provider.GetRequiredService<StressAccessTokenSession>());
        context.Services.AddSingleton<IAccessTokenProvider>(provider =>
            provider.GetRequiredService<StressAccessTokenSession>());
        context.Services.AddSingleton<StressDataRequestHandler>();
        context.Services.AddSingleton<IStressDataRequestProbe>(provider =>
            provider.GetRequiredService<StressDataRequestHandler>());
        context.Services.AddSingleton<IDataRequestHandler>(provider =>
            provider.GetRequiredService<StressDataRequestHandler>());
        context.Services.AddSingleton<IStressRemoteOperations, StressRemoteOperations>();
        context.Services.AddSingleton<IStressRemoteProjection, StressRemoteProjection>();
        context.Services.AddSingleton<IStressDataConnectionFactory, StressDataConnectionFactory>();
        context.Services.AddTransient<RemoteOperationsViewModel>();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        base.OnApplicationInitialization(context);
        context.Services.GetRequiredService<DataClientDescriptorCatalog>().RegisterGenerated<
            global::AtomUI.City.Generated.GeneratedDataClientRegistrar_AtomUI_City_Fixtures_StressCli_07DD3519>();
    }
}
