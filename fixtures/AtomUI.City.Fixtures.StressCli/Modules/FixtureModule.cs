using AtomUI.City.Core.Modularity;
using AtomUI.City.Fixtures.StressCli.Infrastructure;

namespace AtomUI.City.Fixtures.StressCli.Modules;

/// <summary>
/// 所有实战模块的基类：把七阶段生命周期钩子统一写入 LifecycleLedger，
/// 供 Phase A 验证 I1（拓扑序启动）/ I2（逆序停机）/ I10（幂等无重复）。
/// </summary>
public abstract class FixtureModule : ModuleBase
{
    public abstract string FixtureName { get; }

    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        FixtureState.Ledger.Record(FixtureName, LifecycleMilestone.Configuring);
    }

    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        FixtureState.Ledger.Record(FixtureName, LifecycleMilestone.Configured);
    }

    public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
        FixtureState.Ledger.Record(FixtureName, LifecycleMilestone.PreInitializing);
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        FixtureState.Ledger.Record(FixtureName, LifecycleMilestone.Initializing);
    }

    public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
    {
        FixtureState.Ledger.Record(FixtureName, LifecycleMilestone.PostInitializing);
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        FixtureState.Ledger.Record(FixtureName, LifecycleMilestone.Stopping);
        OnShutdownExecuting(context);
        FixtureState.Ledger.Record(FixtureName, LifecycleMilestone.Stopped);
    }

    /// <summary>停机里程碑之间的执行钩子：混沌注入点（抛异常则 Stopped 不会出现）。</summary>
    protected virtual void OnShutdownExecuting(ApplicationShutdownContext context)
    {
    }
}
