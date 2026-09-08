using AtomUI.City.Fixtures.StressCli;
using AtomUI.City.Fixtures.StressCli.Infrastructure;

if (args.Length == 0 || args[0] is "plan" or "--plan")
{
    StressCliProgram.PrintPlan();
    return 0;
}

var report = FixtureState.Report;
var ledger = FixtureState.Ledger;
var runner = new PhaseRunner(report, ledger);
StressCliProgram.RegisterPhases(runner);

var command = args[0].ToLowerInvariant();
return command switch
{
    "run-all" => await runner.RunAllAsync().ConfigureAwait(false),
    "chaos" => await runner.RunPhaseAsync("g").ConfigureAwait(false),
    "routes" => await runner.RunPhaseAsync("h").ConfigureAwait(false),
    "workflow" => await runner.RunPhaseAsync("i").ConfigureAwait(false),
    "localization" => await runner.RunPhaseAsync("j").ConfigureAwait(false),
    "soak" or "localization-soak" => await runner.RunPhaseAsync("k").ConfigureAwait(false),
    "phase" when args.Length > 1 => await runner.RunPhaseAsync(args[1].ToLowerInvariant()).ConfigureAwait(false),
    _ => StressCliProgram.PrintUsage(),
};

internal static class StressCliProgram
{
    public const int ModuleCount = 40;
    public const int ServiceCount = 61;
    public const int EventContractCount = 36;
    public const int StateDefinitionCount = 72;
    public const int ViewModelCount = 16;
    public const int RouteCount = 30;
    public const int InvariantCount = 30;

    public static void PrintPlan()
    {
        Console.WriteLine("AtomUI.City 六模块联合实战验证 —— 测试条件规划");
        Console.WriteLine($"  模块 {ModuleCount} 个（78 条依赖边 + 2 混沌阵地）");
        Console.WriteLine($"  服务 {ServiceCount} 个（Singleton/Scoped/Transient 混合）");
        Console.WriteLine($"  事件契约 {EventContractCount} 条（Current/Background/Serialized）");
        Console.WriteLine($"  状态 {StateDefinitionCount} 个（注册 54 + computed 10 + collection 8）");
        Console.WriteLine($"  ViewModel {ViewModelCount} 个（命令、交互、激活作用域）");
        Console.WriteLine($"  Router 静态路由 {RouteCount} 条（generator + 动态 contribution）");
        Console.WriteLine($"  Localization {AtomUI.City.Fixtures.StressCli.Localization.StressLocalizationCatalog.DescriptorCount} 个语言包（4 culture / 6 scope / 120+ key）");
        Console.WriteLine($"  不变量 {InvariantCount} 条（I01-I30，逐条判定）");
        Console.WriteLine();
        Console.WriteLine("阶段：A 模块图 | B 服务矩阵 | C 事件总线 | D 状态 | E Mvvm | F 联合闭环 | G 混沌 | H Router | I 工作流 | J Localization | K Localization Soak");
        Console.WriteLine("详细名册与不变量定义见 PLAN.md 和 LOCALIZATION-STRESS-PLAN.md");
    }

    public static void RegisterPhases(PhaseRunner runner)
    {
        runner.Register("a", "模块图与生命周期（40 模块 / 78 边 / 逆序停机）", PhaseA.RunAsync);
        runner.Register("b", "服务矩阵（61 服务 / 生命周期语义 / 跨模块依赖）", PhaseB.RunAsync);
        runner.Register("c", "事件总线（36 契约 / 调度 / 错误策略 / 释放退订）", PhaseC.RunAsync);
        runner.Register("d", "状态（54 registry + 10 computed + 8 collection）", PhaseD.RunAsync);
        runner.Register("e", "Mvvm（16 VM / 命令 / 交互 / 激活与零泄漏）", PhaseE.RunAsync);
        runner.Register("f", "联合闭环（事件→状态→VM→命令 ×3 轮）", PhaseF.RunAsync);
        runner.Register("g", "混沌与停机（八场景 + 总报告）", PhaseG.RunAsync);
        runner.Register("h", "Router（30 静态路由 / 管道 / 并发 / 动态贡献）", PhaseH.RunAsync);
        runner.Register("i", "五模块联合工作流（25 轮 + 资源收束）", PhaseI.RunAsync);
        runner.Register("j", "Localization（30 包 / 120+ key / scope / fallback / 故障与撤销）", PhaseJ.RunAsync);
        runner.Register("k", "六模块 Localization Soak（300 轮切换 / 导航 / 并发查找）", PhaseK.RunAsync);
    }

    public static int PrintUsage()
    {
        Console.WriteLine("用法：dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -- [plan | run-all | routes | workflow | localization | localization-soak | chaos | soak | phase <a-k>]");
        return 2;
    }
}
