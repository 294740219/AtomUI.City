using System.Collections.Concurrent;

namespace AtomUI.City.Fixtures.StressCli.Services;

public interface IIdentityDirectory
{
    string SignIn(string subject);
    int ActiveSessions { get; }
}

public sealed class IdentityDirectoryService(ISecurityBoundary security) : IIdentityDirectory
{
    private readonly ConcurrentDictionary<string, string> _sessions = new(StringComparer.Ordinal);

    public int ActiveSessions => _sessions.Count;

    public string SignIn(string subject)
    {
        if (!security.IsTrusted(subject))
        {
            throw new InvalidOperationException($"Subject '{subject}' is not trusted.");
        }

        return _sessions.GetOrAdd(subject, static key => $"session:{key}");
    }
}

public interface ITokenIssuer
{
    Guid InstanceId { get; }
    string Issue(string session);
}

public sealed class TokenIssuerService : ITokenIssuer
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public string Issue(string session) => $"token:{session}:{InstanceId:N}";
}

public interface ITenantDirectory
{
    string Switch(string tenantId);
    string Current { get; }
}

public sealed class TenantDirectoryService(ISettingsStore settings) : ITenantDirectory
{
    private readonly object _gate = new();
    private string _current = "tenant-default";

    public string Current
    {
        get { lock (_gate) { return _current; } }
    }

    public string Switch(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        lock (_gate)
        {
            _current = tenantId;
            settings.Set("tenant.current", tenantId);
            return _current;
        }
    }
}

public interface IProductCatalog
{
    int Count { get; }
    void Upsert(string sku, decimal price);
    decimal GetPrice(string sku);
}

public sealed class ProductCatalogService(ICatalogIndex index) : IProductCatalog
{
    private readonly ConcurrentDictionary<string, decimal> _products = new(StringComparer.Ordinal);
    public int Count => _products.Count;

    public void Upsert(string sku, decimal price)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        _products[sku] = price;
        index.Add(sku);
    }

    public decimal GetPrice(string sku) =>
        _products.TryGetValue(sku, out var price)
            ? price
            : throw new KeyNotFoundException($"Product '{sku}' was not found.");
}

public interface IProductReader
{
    Guid ScopeId { get; }
    decimal ReadPrice(string sku);
}

public sealed class ProductReaderService(IProductCatalog catalog) : IProductReader
{
    public Guid ScopeId { get; } = Guid.NewGuid();
    public decimal ReadPrice(string sku) => catalog.GetPrice(sku);
}

public interface IPriceBook
{
    decimal Quote(string sku, int quantity);
}

public sealed class PriceBookService(IProductCatalog products, ISettingsStore settings) : IPriceBook
{
    public decimal Quote(string sku, int quantity)
    {
        var multiplier = decimal.TryParse(settings.Get("pricing.multiplier"), out var value) ? value : 1m;
        return products.GetPrice(sku) * quantity * multiplier;
    }
}

public interface IPromotionEngine
{
    Guid InstanceId { get; }
    decimal Apply(decimal amount, int customerOrderCount);
}

public sealed class PromotionEngineService : IPromotionEngine
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public decimal Apply(decimal amount, int customerOrderCount) =>
        customerOrderCount >= 3 ? decimal.Round(amount * 0.9m, 2) : amount;
}

public interface IFulfillmentPlanner
{
    Guid ScopeId { get; }
    string Plan(string sku, int quantity);
}

public sealed class FulfillmentPlannerService(IOrderPolicy orders, IProductCatalog products) : IFulfillmentPlanner
{
    public Guid ScopeId { get; } = Guid.NewGuid();

    public string Plan(string sku, int quantity)
    {
        if (!orders.Allow(sku) || quantity <= 0)
        {
            throw new InvalidOperationException("The order cannot be fulfilled.");
        }

        _ = products.GetPrice(sku);
        return $"plan:{sku}:{quantity}:{ScopeId:N}";
    }
}

public interface IPickListStore
{
    Guid ScopeId { get; }
    int Count { get; }
    void Add(string plan);
}

public sealed class PickListStoreService : IPickListStore
{
    private readonly List<string> _plans = [];
    public Guid ScopeId { get; } = Guid.NewGuid();
    public int Count => _plans.Count;
    public void Add(string plan) => _plans.Add(plan);
}

public interface IShippingQuote
{
    Guid InstanceId { get; }
    decimal Quote(int quantity);
}

public sealed class ShippingQuoteService(IStockPolicy stock) : IShippingQuote
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public decimal Quote(int quantity) => 4m + quantity + stock.LowWatermark / 10m;
}

public interface IShipmentTracker
{
    int Dispatched { get; }
    string Dispatch(string plan);
}

public sealed class ShipmentTrackerService(IScheduler scheduler) : IShipmentTracker
{
    private int _dispatched;
    public int Dispatched => Volatile.Read(ref _dispatched);

    public string Dispatch(string plan)
    {
        scheduler.Schedule($"track:{plan}");
        var sequence = Interlocked.Increment(ref _dispatched);
        return $"shipment:{sequence}";
    }
}

public interface IPaymentGateway
{
    int Authorized { get; }
    string Authorize(string subject, decimal amount);
}

public sealed class PaymentGatewayService(
    ISecurityBoundary security,
    IBillingCalculator billing,
    ISequenceService sequence) : IPaymentGateway
{
    private int _authorized;
    public int Authorized => Volatile.Read(ref _authorized);

    public string Authorize(string subject, decimal amount)
    {
        if (!security.IsTrusted(subject) || amount <= 0)
        {
            throw new InvalidOperationException("Payment authorization was rejected.");
        }

        _ = billing.Settle(amount);
        Interlocked.Increment(ref _authorized);
        return $"pay:{sequence.Next()}";
    }
}

public interface IPaymentLedger
{
    Guid ScopeId { get; }
    decimal Total { get; }
    void Capture(decimal amount);
}

public sealed class PaymentLedgerService : IPaymentLedger
{
    private decimal _total;
    public Guid ScopeId { get; } = Guid.NewGuid();
    public decimal Total => _total;
    public void Capture(decimal amount) => _total += amount;
}

public interface IReturnPolicy
{
    bool CanReturn(decimal amount, int ageInDays);
}

public sealed class ReturnPolicyService : IReturnPolicy
{
    public bool CanReturn(decimal amount, int ageInDays) => amount > 0 && ageInDays is >= 0 and <= 30;
}

public interface IReturnCaseStore
{
    Guid ScopeId { get; }
    int OpenCases { get; }
    string Open(string paymentId);
}

public sealed class ReturnCaseStoreService : IReturnCaseStore
{
    private int _openCases;
    public Guid ScopeId { get; } = Guid.NewGuid();
    public int OpenCases => _openCases;

    public string Open(string paymentId)
    {
        _openCases++;
        return $"return:{paymentId}:{_openCases}";
    }
}

public interface ISearchIndex
{
    int Indexed { get; }
    void Index(string sku);
    IReadOnlyList<string> Search(string term);
}

public sealed class SearchIndexService(ITelemetrySink telemetry) : ISearchIndex
{
    private readonly ConcurrentDictionary<string, byte> _items = new(StringComparer.OrdinalIgnoreCase);
    public int Indexed => _items.Count;

    public void Index(string sku)
    {
        _items[sku] = 0;
        telemetry.Record($"search.index:{sku}");
    }

    public IReadOnlyList<string> Search(string term) =>
        _items.Keys.Where(item => item.Contains(term, StringComparison.OrdinalIgnoreCase)).Order().ToArray();
}

public interface ISearchSession
{
    Guid ScopeId { get; }
    int Queries { get; }
    IReadOnlyList<string> Execute(ISearchIndex index, string term);
}

public sealed class SearchSessionService : ISearchSession
{
    private int _queries;
    public Guid ScopeId { get; } = Guid.NewGuid();
    public int Queries => _queries;

    public IReadOnlyList<string> Execute(ISearchIndex index, string term)
    {
        _queries++;
        return index.Search(term);
    }
}

public interface IRecommendationEngine
{
    Guid InstanceId { get; }
    string Recommend(IReadOnlyList<string> candidates);
}

public sealed class RecommendationEngineService : IRecommendationEngine
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public string Recommend(IReadOnlyList<string> candidates) => candidates.FirstOrDefault() ?? "none";
}

public interface IFraudScorer
{
    Guid InstanceId { get; }
    decimal Score(string subject, decimal amount);
}

public sealed class FraudScorerService(ISecurityBoundary security) : IFraudScorer
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public decimal Score(string subject, decimal amount) => security.IsTrusted(subject) ? Math.Min(1m, amount / 10000m) : 1m;
}

public interface ISupportDesk
{
    int OpenTickets { get; }
    string Open(string customer, string subject);
}

public sealed class SupportDeskService(IAuditTrail audit, ICustomerDirectory customers) : ISupportDesk
{
    private int _openTickets;
    public int OpenTickets => Volatile.Read(ref _openTickets);

    public string Open(string customer, string subject)
    {
        customers.Register(customer);
        audit.Append($"support:{customer}:{subject}");
        return $"ticket:{Interlocked.Increment(ref _openTickets)}";
    }
}

public interface ISupportSession
{
    Guid ScopeId { get; }
    int Messages { get; }
    void Append(string message);
}

public sealed class SupportSessionService : ISupportSession
{
    private int _messages;
    public Guid ScopeId { get; } = Guid.NewGuid();
    public int Messages => _messages;
    public void Append(string message) => _messages++;
}

public interface IWorkflowEngine
{
    int Started { get; }
    string Start(string operation);
}

public sealed class WorkflowEngineService(IScheduler scheduler, IMessageQueue queue) : IWorkflowEngine
{
    private int _started;
    public int Started => Volatile.Read(ref _started);

    public string Start(string operation)
    {
        scheduler.Schedule(operation);
        queue.Enqueue(operation);
        return $"workflow:{Interlocked.Increment(ref _started)}";
    }
}

public interface IWorkflowRun
{
    Guid ScopeId { get; }
    int Step { get; }
    void Advance();
}

public sealed class WorkflowRunService : IWorkflowRun
{
    private int _step;
    public Guid ScopeId { get; } = Guid.NewGuid();
    public int Step => _step;
    public void Advance() => _step++;
}

public interface INavigationAudit
{
    IReadOnlyList<string> Entries { get; }
    void Record(string entry);
}

public sealed class NavigationAuditService : INavigationAudit
{
    private readonly ConcurrentQueue<string> _entries = new();
    public IReadOnlyList<string> Entries => _entries.ToArray();
    public void Record(string entry) => _entries.Enqueue(entry);
}

public sealed record OperationsReceipt(
    string WorkflowId,
    string PaymentId,
    string SupportTicket,
    decimal Amount);

public interface IOperationsFacade
{
    OperationsReceipt Execute(string subject, string sku, int quantity);
}

public sealed class OperationsFacadeService(
    IProductCatalog products,
    IPriceBook prices,
    IPaymentGateway payments,
    IReturnPolicy returns,
    ISupportDesk support,
    IWorkflowEngine workflows,
    INavigationAudit navigation) : IOperationsFacade
{
    public OperationsReceipt Execute(string subject, string sku, int quantity)
    {
        _ = products.GetPrice(sku);
        var amount = prices.Quote(sku, quantity);
        var workflowId = workflows.Start($"order:{sku}");
        var paymentId = payments.Authorize(subject, amount);
        _ = returns.CanReturn(amount, 0);
        var ticket = support.Open(subject, $"order:{sku}");
        navigation.Record($"operation:{workflowId}:{paymentId}");
        return new OperationsReceipt(workflowId, paymentId, ticket, amount);
    }
}
