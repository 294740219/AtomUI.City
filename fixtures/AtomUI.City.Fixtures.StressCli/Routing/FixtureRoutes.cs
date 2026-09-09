using AtomUI.City.Fixtures.StressCli.ViewModels;
using AtomUI.City.Routing;

namespace AtomUI.City.Fixtures.StressCli.Routing;

public readonly record struct OrderRouteParameters(int Id);
public readonly record struct ProductRouteParameters(string Sku);
public readonly record struct SearchRouteParameters(string Term);

[RouteMap]
public static partial class FixtureRoutes
{
    [LayoutRoute(typeof(ShellViewModel), Id = "fixtures.routes.shell")]
    public static partial RouteReference Shell();

    [IndexRoute(typeof(DashboardViewModel), Id = "fixtures.routes.dashboard", Parent = nameof(Shell))]
    public static partial RouteReference Dashboard();

    [RouteGroup("commerce", Id = "fixtures.routes.commerce", Parent = nameof(Shell))]
    public static partial RouteReference Commerce();

    [Route("orders", typeof(OrdersViewModel), Id = "fixtures.routes.orders", Parent = nameof(Commerce))]
    public static partial RouteReference Orders();

    [Route("{id:int:min(1)}", typeof(OrdersViewModel), Id = "fixtures.routes.order-details", Parent = nameof(Orders))]
    [RouteMatchPolicies(typeof(FixtureRouteMatchPolicy))]
    [RouteGuards(typeof(FixtureRouteGuard))]
    [RouteResolvers(typeof(FixtureRouteResolver))]
    [RouteMiddleware(typeof(FixtureRouteMiddleware))]
    public static partial RouteReference<OrderRouteParameters> OrderDetails();

    [Route("inventory", typeof(InventoryViewModel), Id = "fixtures.routes.inventory", Parent = nameof(Commerce))]
    public static partial RouteReference Inventory();

    [Route("customers", typeof(CustomersViewModel), Id = "fixtures.routes.customers", Parent = nameof(Commerce))]
    public static partial RouteReference Customers();

    [Route("billing", typeof(BillingViewModel), Id = "fixtures.routes.billing", Parent = nameof(Commerce))]
    public static partial RouteReference Billing();

    [Route("payments", typeof(PaymentsViewModel), Id = "fixtures.routes.payments", Parent = nameof(Commerce))]
    public static partial RouteReference Payments();

    [Route("returns", typeof(OrdersViewModel), Id = "fixtures.routes.returns", Parent = nameof(Commerce))]
    public static partial RouteReference Returns();

    [RouteGroup("catalog", Id = "fixtures.routes.catalog", Parent = nameof(Shell))]
    public static partial RouteReference Catalog();

    [Route("products", typeof(ProductsViewModel), Id = "fixtures.routes.products", Parent = nameof(Catalog))]
    public static partial RouteReference Products();

    [Route("{sku:regex(^SKU-[0-9]{4}$)}", typeof(ProductsViewModel), Id = "fixtures.routes.product-details", Parent = nameof(Products))]
    public static partial RouteReference<ProductRouteParameters> ProductDetails();

    [Route("pricing", typeof(ProductsViewModel), Id = "fixtures.routes.pricing", Parent = nameof(Catalog))]
    public static partial RouteReference Pricing();

    [Route("promotions", typeof(ProductsViewModel), Id = "fixtures.routes.promotions", Parent = nameof(Catalog))]
    public static partial RouteReference Promotions();

    [RouteGroup("operations", Id = "fixtures.routes.operations", Parent = nameof(Shell))]
    public static partial RouteReference Operations();

    [Route("fulfillment", typeof(FulfillmentViewModel), Id = "fixtures.routes.fulfillment", Parent = nameof(Operations))]
    public static partial RouteReference Fulfillment();

    [Route("shipping", typeof(FulfillmentViewModel), Id = "fixtures.routes.shipping", Parent = nameof(Operations))]
    public static partial RouteReference Shipping();

    [Route("reports", typeof(ReportViewModel), Id = "fixtures.routes.reports", Parent = nameof(Operations))]
    public static partial RouteReference Reports();

    [Route("analytics", typeof(DashboardViewModel), Id = "fixtures.routes.analytics", Parent = nameof(Operations))]
    public static partial RouteReference Analytics();

    [Route("audit", typeof(AuditViewModel), Id = "fixtures.routes.audit", Parent = nameof(Operations))]
    public static partial RouteReference Audit();

    [Route("support", typeof(SupportViewModel), Id = "fixtures.routes.support", Parent = nameof(Operations))]
    public static partial RouteReference Support();

    [Route("workflow", typeof(WorkflowViewModel), Id = "fixtures.routes.workflow", Parent = nameof(Operations))]
    [RouteMiddleware(typeof(FixtureRouteMiddleware))]
    public static partial RouteReference Workflow();

    [Route("remote-data", typeof(RemoteOperationsViewModel), Id = "fixtures.routes.remote-data", Parent = nameof(Operations))]
    public static partial RouteReference RemoteData();

    [Route("settings", typeof(SettingsViewModel), Id = "fixtures.routes.settings", Parent = nameof(Shell))]
    public static partial RouteReference Settings();

    [Route("search/{term}", typeof(SearchViewModel), Id = "fixtures.routes.search", Parent = nameof(Shell))]
    public static partial RouteReference<SearchRouteParameters> Search();

    [Route("search/{term}", typeof(SearchViewModel), Id = "fixtures.routes.premium-search", Parent = nameof(Shell))]
    [RouteMatchPolicies(typeof(FixtureRouteMatchPolicy))]
    public static partial RouteReference<SearchRouteParameters> PremiumSearch();

    [Route("recommendations", typeof(SearchViewModel), Id = "fixtures.routes.recommendations", Parent = nameof(Shell))]
    public static partial RouteReference Recommendations();

    [Route("notifications", typeof(NotificationsViewModel), Id = "fixtures.routes.notifications", Parent = nameof(Shell), Outlet = "side")]
    public static partial RouteReference Notifications();

    [RedirectRoute("legacy-orders", Id = "fixtures.routes.legacy-orders", Parent = nameof(Shell), Target = nameof(Orders))]
    public static partial RouteReference LegacyOrders();

    [RouteExtensionPoint("fixtures.extensions.operations", Id = "fixtures.routes.operations-extension", Parent = nameof(Operations))]
    public static partial RouteExtensionPoint OperationsExtensionPoint();
}
