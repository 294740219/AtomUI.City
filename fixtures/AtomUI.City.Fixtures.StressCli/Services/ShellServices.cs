using AtomUI.City.Fixtures.StressCli;
using AtomUI.City.Fixtures.StressCli.Services;

namespace AtomUI.City.Fixtures.StressCli.Services;

// StoreFront(1) —— 跨模块依赖：Customers + Inventory

public interface IStoreFrontFacade
{
    ICustomerDirectory Directory { get; }
    IStockPolicy Stock { get; }
}

public sealed class StoreFrontFacadeService : IStoreFrontFacade
{
    private readonly ICustomerDirectory _directory;
    private readonly IStockPolicy _stock;

    public StoreFrontFacadeService(ICustomerDirectory directory, IStockPolicy stock)
    {
        _directory = directory;
        _stock = stock;
    }

    public ICustomerDirectory Directory => _directory;
    public IStockPolicy Stock => _stock;
}

// Shell(1)

public interface IShellNavigator
{
    string Current { get; }
    void Navigate(string route);
}

public sealed class ShellNavigatorService : IShellNavigator
{
    private string _current = "home";
    public string Current => _current;
    public void Navigate(string route) => _current = route;
}

// Faulty(1) —— 读取混沌开关

public interface IFaultInjector
{
    bool ShouldFaultOnInit { get; }
    bool ShouldFaultOnShutdown { get; }
}

public sealed class FaultInjectorService : IFaultInjector
{
    public bool ShouldFaultOnInit => FixtureState.FaultOnInit;
    public bool ShouldFaultOnShutdown => FixtureState.FaultOnShutdown;
}

// Flaky(1)

public interface IFlakyProbe
{
    int Calls { get; }
    void Probe();
}

public sealed class FlakyProbeService : IFlakyProbe
{
    private int _calls;
    public int Calls => _calls;
    public void Probe() => Interlocked.Increment(ref _calls);
}
