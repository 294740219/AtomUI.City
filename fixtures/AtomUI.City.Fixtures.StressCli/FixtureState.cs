using AtomUI.City.Fixtures.StressCli.Infrastructure;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>
/// 实战夹具的共享静态状态。夹具代码允许使用静态载体——它不是产品代码。
/// </summary>
public static class FixtureState
{
    public static LifecycleLedger Ledger { get; } = new();

    public static FixtureReport Report { get; } = new();

    /// <summary>FaultyModule 初始化失败开关（Phase G 混沌）。</summary>
    public static bool FaultOnInit { get; set; }

    /// <summary>FlakyModule 停机失败开关（Phase G 混沌）。</summary>
    public static bool FaultOnShutdown { get; set; }

    public static void Reset()
    {
        Ledger.Clear();
        FaultOnInit = false;
        FaultOnShutdown = false;
    }
}
