using AtomUI.City.Core.Modularity;
using AtomUI.City.Fixtures.StressCli;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli.Modules;

// 混沌阵地 —— 可控失败开关，默认关闭；Phase G 打开后验证 Host 补偿与聚合诊断。

[DependsOn(typeof(FoundationModule))]
public sealed class FaultyModule : FixtureModule
{
    public override string FixtureName => "Faulty";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IFaultInjector, FaultInjectorService>();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        base.OnApplicationInitialization(context);

        if (FixtureState.FaultOnInit)
        {
            throw new InvalidOperationException("FaultyModule was configured to fail during initialization.");
        }
    }
}

[DependsOn(typeof(FoundationModule))]
public sealed class FlakyModule : FixtureModule
{
    public override string FixtureName => "Flaky";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<IFlakyProbe, FlakyProbeService>();
    }

    protected override void OnShutdownExecuting(ApplicationShutdownContext context)
    {
        if (FixtureState.FaultOnShutdown)
        {
            throw new InvalidOperationException("FlakyModule was configured to fail during shutdown.");
        }
    }
}
