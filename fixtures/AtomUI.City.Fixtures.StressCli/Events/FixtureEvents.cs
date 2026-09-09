using AtomUI.City.EventBus;
using AtomUI.City.Fixtures.StressCli.DataIntegration;
using AtomUI.City.Fixtures.StressCli.Modules;

namespace AtomUI.City.Fixtures.StressCli.Events;

// 43 条事件契约：契约 id + 所有者模块（[EventContract] 特性由 generator/校验器消费）。

[EventContract("fixtures.events.orders.submitted", typeof(OrdersModule))]
public sealed record OrderSubmitted(string Sku, int Quantity, decimal Price);

[EventContract("fixtures.events.orders.confirmed", typeof(OrdersModule))]
public sealed record OrderConfirmed(string Sku);

[EventContract("fixtures.events.inventory.reserved", typeof(InventoryModule))]
public sealed record InventoryReserved(string Sku, int Amount);

[EventContract("fixtures.events.inventory.low", typeof(InventoryModule))]
public sealed record InventoryLow(string Sku, int Remaining);

[EventContract("fixtures.events.customer.registered", typeof(CustomersModule))]
public sealed record CustomerRegistered(string Name);

[EventContract("fixtures.events.billing.settled", typeof(BillingModule))]
public sealed record BillingSettled(decimal Amount);

[EventContract("fixtures.events.billing.tax", typeof(BillingTaxModule))]
public sealed record TaxComputed(decimal Rate);

[EventContract("fixtures.events.report.generated", typeof(ReportingModule))]
public sealed record ReportGenerated(string Title);

[EventContract("fixtures.events.analytics.refreshed", typeof(AnalyticsModule))]
public sealed record AnalyticsRefreshed(int SourceCount);

[EventContract("fixtures.events.notification.dispatched", typeof(NotificationsModule))]
public sealed record NotificationDispatched(string Message);

[EventContract("fixtures.events.audit.appended", typeof(AuditModule))]
public sealed record AuditAppended(string Entry);

[EventContract("fixtures.events.dashboard.updated", typeof(DashboardModule))]
public sealed record DashboardUpdated(string View);

[EventContract("fixtures.events.telemetry.sampled", typeof(TelemetryModule))]
public sealed record TelemetrySampled(int Sample);

[EventContract("fixtures.events.schedule.fired", typeof(SchedulingModule))]
public sealed record ScheduleFired(string Job);

[EventContract("fixtures.events.settings.changed", typeof(SettingsModule))]
public sealed record SettingsChanged(string Key);

[EventContract("fixtures.events.catalog.rebuilt", typeof(DataCatalogModule))]
public sealed record CatalogRebuilt(int Revision);

[EventContract("fixtures.events.message.journaled", typeof(MessagingModule))]
public sealed record MessageJournaled(string Entry);

[EventContract("fixtures.events.workspace.saved", typeof(WorkspaceModule))]
public sealed record WorkspaceSaved(string Workspace);

[EventContract("fixtures.events.security.crossed", typeof(SecurityModule))]
public sealed record SecurityCrossed(string Subject);

[EventContract("fixtures.events.fault.injected", typeof(FaultyModule))]
public sealed record FaultInjected(string Scenario);

[EventContract("fixtures.events.identity.signed-in", typeof(IdentityModule))]
public sealed record UserSignedIn(string Subject, string SessionId);

[EventContract("fixtures.events.tenant.switched", typeof(TenancyModule))]
public sealed record TenantSwitched(string TenantId, int Revision);

[EventContract("fixtures.events.product.indexed", typeof(ProductModule))]
public sealed record ProductIndexed(string Sku, int ProductCount);

[EventContract("fixtures.events.pricing.changed", typeof(PricingModule))]
public sealed record PriceChanged(string Sku, decimal Price);

[EventContract("fixtures.events.promotion.applied", typeof(PromotionsModule))]
public sealed record PromotionApplied(string Sku, decimal DiscountedAmount);

[EventContract("fixtures.events.fulfillment.planned", typeof(FulfillmentModule))]
public sealed record FulfillmentPlanned(string OrderId, string PlanId);

[EventContract("fixtures.events.picklist.created", typeof(FulfillmentModule))]
public sealed record PickListCreated(string PlanId, int ItemCount);

[EventContract("fixtures.events.shipping.quoted", typeof(ShippingModule))]
public sealed record ShippingQuoted(string OrderId, decimal Amount);

[EventContract("fixtures.events.shipment.dispatched", typeof(ShippingModule))]
public sealed record ShipmentDispatched(string OrderId, string ShipmentId);

[EventContract("fixtures.events.payment.authorized", typeof(PaymentsModule))]
public sealed record PaymentAuthorized(string OrderId, string PaymentId, decimal Amount);

[EventContract("fixtures.events.payment.captured", typeof(PaymentsModule))]
public sealed record PaymentCaptured(string PaymentId, decimal Amount);

[EventContract("fixtures.events.return.requested", typeof(ReturnsModule))]
public sealed record ReturnRequested(string PaymentId, string ReturnId);

[EventContract("fixtures.events.search.executed", typeof(SearchModule))]
public sealed record SearchExecuted(string Term, int ResultCount);

[EventContract("fixtures.events.recommendation.produced", typeof(RecommendationsModule))]
public sealed record RecommendationProduced(string Subject, string Sku);

[EventContract("fixtures.events.fraud.flagged", typeof(FraudModule))]
public sealed record FraudFlagged(string Subject, decimal Score);

[EventContract("fixtures.events.support.opened", typeof(SupportModule))]
public sealed record SupportTicketOpened(string Subject, string TicketId);

[EventContract("fixtures.events.remote.product-loaded", typeof(OperationsModule))]
public sealed record RemoteProductLoaded(StressProductSnapshot Product);

[EventContract("fixtures.events.remote.order-submitted", typeof(OperationsModule))]
public sealed record RemoteOrderSubmitted(StressOrderReceipt Receipt);

[EventContract("fixtures.events.remote.data-failed", typeof(OperationsModule))]
public sealed record RemoteDataFailed(string Operation, string ErrorKind, string MessageKey);

[EventContract("fixtures.events.remote.inventory-changed", typeof(OperationsModule))]
public sealed record RemoteInventoryChanged(string Sku, int Quantity, long Sequence, string PrincipalRevision);

[EventContract("fixtures.events.remote.price-changed", typeof(OperationsModule))]
public sealed record RemotePriceChanged(string Sku, decimal Price, long Sequence, string PrincipalRevision);

[EventContract("fixtures.events.remote.shipment-progressed", typeof(OperationsModule))]
public sealed record RemoteShipmentProgressed(string OrderId, string Status, long Sequence, string PrincipalRevision);

[EventContract("fixtures.events.remote.principal-switched", typeof(OperationsModule))]
public sealed record RemotePrincipalSwitched(string PreviousRevision, string CurrentPrincipal, string CurrentRevision);
