namespace AtomUI.City.Fixtures.StressCli.Services;

// Scheduling(2)

public interface IScheduler
{
    int Scheduled { get; }
    void Schedule(string job);
}

public sealed class SchedulerService : IScheduler
{
    private int _scheduled;
    public int Scheduled => _scheduled;
    public void Schedule(string job) => Interlocked.Increment(ref _scheduled);
}

public interface IScheduleStore
{
    IReadOnlyList<string> Pending { get; }
    void Enqueue(string job);
}

public sealed class ScheduleStoreService : IScheduleStore
{
    private readonly List<string> _pending = [];
    public IReadOnlyList<string> Pending => _pending;
    public void Enqueue(string job) => _pending.Add(job);
}

// Orders(3)

public interface IOrderPolicy
{
    bool Allow(string sku);
}

public sealed class OrderPolicyService : IOrderPolicy
{
    public bool Allow(string sku) => !string.IsNullOrWhiteSpace(sku);
}

public interface IOrderPricing
{
    decimal Price(string sku);
}

public sealed class PricingService : IOrderPricing
{
    public decimal Price(string sku) => 10m + sku.Length;
}

public interface IOrderValidator
{
    bool Validate(string sku);
}

public sealed class OrderValidatorService : IOrderValidator
{
    public bool Validate(string sku) => sku.StartsWith("SKU-", StringComparison.Ordinal);
}

// Inventory(1)

public interface IStockPolicy
{
    int LowWatermark { get; }
}

public sealed class StockPolicyService : IStockPolicy
{
    public int LowWatermark => 10;
}

// Customers(1)

public interface ICustomerDirectory
{
    int Registered { get; }
    void Register(string name);
}

public sealed class CustomerDirectoryService : ICustomerDirectory
{
    private int _registered;
    public int Registered => _registered;
    public void Register(string name) => Interlocked.Increment(ref _registered);
}

// Billing(2) —— BillingCalculator 依赖跨模块的 ITaxPolicy（Transient）

public interface IBillingCalculator
{
    decimal Settle(decimal amount);
}

public sealed class BillingCalculatorService : IBillingCalculator
{
    private readonly ITaxPolicy _taxPolicy;
    public BillingCalculatorService(ITaxPolicy taxPolicy) => _taxPolicy = taxPolicy;
    public decimal Settle(decimal amount) => amount + amount * _taxPolicy.Rate();
}

public interface IBillingLedger
{
    int Settled { get; }
    void Record(decimal amount);
}

public sealed class BillingLedgerService : IBillingLedger
{
    private readonly List<decimal> _amounts = [];
    public int Settled => _amounts.Count;
    public void Record(decimal amount) => _amounts.Add(amount);
}

// BillingTax(1)

public interface ITaxPolicy
{
    decimal Rate();
}

public sealed class TaxPolicyService : ITaxPolicy
{
    public decimal Rate() => 0.19m;
}
