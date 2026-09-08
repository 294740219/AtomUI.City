using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.Modules;
using AtomUI.City.Fixtures.StressCli.Localization;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Localization;
using AtomUI.City.Routing;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli.Infrastructure;

/// <summary>
/// 实战压测的统一 Host 构建入口：六模块基础设施 + 40 个业务模块 + 固定 ApplicationId。
/// </summary>
public static class StressHost
{
    public const string ApplicationId = "fixtures.stress";
    public const string ApplicationName = "AtomUI.City.Fixtures.StressCli";

    public static IApplicationHostBuilder CreateBuilder()
    {
        var builder = ApplicationHost.CreateBuilder([]);

        builder.ConfigureHost(options =>
        {
            options.ApplicationId = ApplicationId;
            options.ApplicationName = ApplicationName;
        });

        builder.ConfigureServices(services =>
        {
            services.AddState();
            services.AddRouting(AtomUI.City.Generated.GeneratedRoutingRouteManifest.CreateDescriptors());
            services.AddSingleton<StressLanguagePackageProvider>();
            services.AddSingleton<ILanguagePackageProvider>(serviceProvider =>
                serviceProvider.GetRequiredService<StressLanguagePackageProvider>());
            services.AddSingleton<StressPresentationLocalizationBridge>();
            services.AddSingleton<IPresentationLocalizationBridge>(serviceProvider =>
                serviceProvider.GetRequiredService<StressPresentationLocalizationBridge>());
            services.AddLocalization(options =>
            {
                options.DefaultCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
                options.DefaultUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
                foreach (var descriptor in StressLocalizationCatalog.CreateDescriptors())
                {
                    options.LanguagePackages.Add(descriptor);
                }
            });
            services.AddSingleton(_ => LifecycleScope.CreateRoot(
                LifecycleScopeKind.Application,
                "fixtures-viewmodel-events"));
        });

        builder
            .UseModule<EventBusModule>()
            .UseModule<RoutingModule>()
            .UseModule<FoundationModule>()
            .UseModule<TelemetryModule>()
            .UseModule<SettingsModule>()
            .UseModule<DataCatalogModule>()
            .UseModule<MessagingModule>()
            .UseModule<SecurityModule>()
            .UseModule<SchedulingModule>()
            .UseModule<IdentityModule>()
            .UseModule<TenancyModule>()
            .UseModule<TelemetryArchiveModule>()
            .UseModule<CatalogIndexModule>()
            .UseModule<OrdersModule>()
            .UseModule<InventoryModule>()
            .UseModule<CustomersModule>()
            .UseModule<ProductModule>()
            .UseModule<BillingModule>()
            .UseModule<ReportingModule>()
            .UseModule<BillingTaxModule>()
            .UseModule<PricingModule>()
            .UseModule<SearchModule>()
            .UseModule<FraudModule>()
            .UseModule<FulfillmentModule>()
            .UseModule<AnalyticsModule>()
            .UseModule<NotificationsModule>()
            .UseModule<AuditModule>()
            .UseModule<ShippingModule>()
            .UseModule<PromotionsModule>()
            .UseModule<PaymentsModule>()
            .UseModule<DashboardModule>()
            .UseModule<ReturnsModule>()
            .UseModule<RecommendationsModule>()
            .UseModule<SupportModule>()
            .UseModule<WorkflowModule>()
            .UseModule<WorkspaceModule>()
            .UseModule<ShellModule>()
            .UseModule<StoreFrontModule>()
            .UseModule<NavigationModule>()
            .UseModule<OperationsModule>()
            .UseModule<FaultyModule>()
            .UseModule<FlakyModule>();

        return builder;
    }
}
