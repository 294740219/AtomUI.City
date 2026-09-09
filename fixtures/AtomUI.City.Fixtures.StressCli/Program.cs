using AtomUI.City.Fixtures.StressCli;
using AtomUI.City.Fixtures.StressCli.Infrastructure;

if (args.Length == 0 || args[0] is "plan" or "--plan")
{
    StressCliProgram.PrintPlan();
    return 0;
}

var command = args[0].ToLowerInvariant();
if (command == "phase" && args.Length < 2)
{
    return StressCliProgram.PrintUsage();
}

var defaultProfile = command is "localization-extreme" or "data-extreme"
    ? StressProfile.Extreme
    : StressProfile.Standard;
var optionArguments = command == "phase" ? args.Skip(2).ToArray() : args.Skip(1).ToArray();
if (!StressExecutionOptions.TryParse(optionArguments, defaultProfile, out var options, out var optionError))
{
    Console.Error.WriteLine(optionError);
    return StressCliProgram.PrintUsage();
}

var report = FixtureState.Report;
var ledger = FixtureState.Ledger;
var runner = new PhaseRunner(report, ledger, options.PhaseTimeout);
StressCliProgram.RegisterPhases(runner, options);

Console.WriteLine(
    $"Stress profile={options.Profile} seed={options.Seed} operations={options.Operations} " +
    $"workers={options.Workers} timeout={options.PhaseTimeout}");
return command switch
{
    "run-all" => await runner.RunAllAsync().ConfigureAwait(false),
    "chaos" => await runner.RunPhaseAsync("g").ConfigureAwait(false),
    "routes" => await runner.RunPhaseAsync("h").ConfigureAwait(false),
    "workflow" => await runner.RunPhaseAsync("i").ConfigureAwait(false),
    "localization" => await runner.RunPhaseAsync("j").ConfigureAwait(false),
    "soak" or "localization-soak" => await runner.RunPhaseAsync("k").ConfigureAwait(false),
    "localization-providers" => await runner.RunPhaseAsync("l").ConfigureAwait(false),
    "localization-races" => await runner.RunPhaseAsync("m").ConfigureAwait(false),
    "localization-chaos" => await runner.RunPhaseAsync("n").ConfigureAwait(false),
    "localization-lifecycle" => await runner.RunPhaseAsync("o").ConfigureAwait(false),
    "data-workflow" => await runner.RunPhaseAsync("p").ConfigureAwait(false),
    "data-realtime" => await runner.RunPhaseAsync("q").ConfigureAwait(false),
    "data-chaos" => await runner.RunPhaseAsync("r").ConfigureAwait(false),
    "data-suite" or "data-extreme" => await runner
        .RunPhasesAsync(["p", "q", "r"])
        .ConfigureAwait(false),
    "localization-suite" or "localization-extreme" => await runner
        .RunPhasesAsync(["j", "k", "l", "m", "n", "o"])
        .ConfigureAwait(false),
    "phase" => await runner.RunPhaseAsync(args[1].ToLowerInvariant()).ConfigureAwait(false),
    _ => StressCliProgram.PrintUsage(),
};

internal static class StressCliProgram
{
    public const int ModuleCount = 40;
    public const int ServiceCount = 67;
    public const int EventContractCount = 43;
    public const int StateDefinitionCount = 84;
    public const int BaseViewModelCount = 16;
    public const int ViewModelCount = 17;
    public const int RouteCount = 31;
    public const int InvariantCount = 108;

    public static void PrintPlan()
    {
        Console.WriteLine("AtomUI.City 七模块联合实战验证 —— 测试条件规划");
        Console.WriteLine($"  模块 {ModuleCount} 个（78 条依赖边 + 2 混沌阵地）");
        Console.WriteLine($"  服务 {ServiceCount} 个（Singleton/Scoped/Transient 混合）");
        Console.WriteLine($"  事件契约 {EventContractCount} 条（Current/Background/Serialized）");
        Console.WriteLine($"  状态 {StateDefinitionCount} 个（注册 66 + computed 10 + collection 8）");
        Console.WriteLine($"  ViewModel {ViewModelCount} 个（命令、交互、激活作用域）");
        Console.WriteLine($"  Router 静态路由 {RouteCount} 条（generator + 动态 contribution）");
        Console.WriteLine($"  Localization {AtomUI.City.Fixtures.StressCli.Localization.StressLocalizationCatalog.DescriptorCount} 个语言包（11 culture / 6 scope / 300+ key）");
        Console.WriteLine("  Data 真实本地 HTTP / gRPC / SignalR（缓存、并发、账号切换、停机 drain）");
        Console.WriteLine($"  不变量至少 {InvariantCount} 条（基础、Provider、竞态、混沌和生命周期逐条判定）");
        Console.WriteLine();
        Console.WriteLine("阶段：A-I 五模块 | J-O Localization | P Data 纵向链 | Q Realtime | R Data Chaos");
        Console.WriteLine("详细名册与不变量定义见 PLAN.md、LOCALIZATION-STRESS-PLAN.md 和 DATA-INTEGRATION-STRESS-PLAN.md");
    }

    public static void RegisterPhases(PhaseRunner runner, StressExecutionOptions options)
    {
        runner.Register("a", "模块图与生命周期（40 模块 / 78 边 / 逆序停机）", PhaseA.RunAsync);
        runner.Register("b", "服务矩阵（67 服务 / 生命周期语义 / 跨模块依赖）", PhaseB.RunAsync);
        runner.Register("c", "事件总线（43 契约 / 调度 / 错误策略 / 释放退订）", PhaseC.RunAsync);
        runner.Register("d", "状态（66 registry + 10 computed + 8 collection）", PhaseD.RunAsync);
        runner.Register("e", "Mvvm（17 VM / 命令 / 交互 / 激活与零泄漏）", PhaseE.RunAsync);
        runner.Register("f", "联合闭环（事件→状态→VM→命令 ×3 轮）", PhaseF.RunAsync);
        runner.Register("g", "混沌与停机（八场景 + 总报告）", PhaseG.RunAsync);
        runner.Register("h", "Router（31 静态路由 / 管道 / 并发 / 动态贡献）", PhaseH.RunAsync);
        runner.Register("i", "五模块联合工作流（25 轮 + 资源收束）", PhaseI.RunAsync);
        runner.Register("j", "Localization（93 包 / 388 key / scope / fallback / 故障与撤销）", PhaseJ.RunAsync);
        runner.Register("k", $"六模块 Localization Soak（{options.SoakIterations} 轮切换 / 导航 / 并发查找）", cancellationToken => PhaseK.RunAsync(options, cancellationToken));
        runner.Register("l", "真实 Provider（File / Assembly / Generator / 撤销）", PhaseL.RunAsync);
        runner.Register("m", $"确定性竞态（每类最多 {options.RaceIterations} 轮）", cancellationToken => PhaseM.RunAsync(options, cancellationToken));
        runner.Register("n", $"Seeded Chaos（{options.Operations} 操作 / {options.Workers} workers）", cancellationToken => PhaseN.RunAsync(options, cancellationToken));
        runner.Register("o", $"生命周期收束（{options.HostCycles} 次完整 Host 周期）", cancellationToken => PhaseO.RunAsync(options, cancellationToken));
        runner.Register("p", $"Data 七模块业务纵向链（{options.DataIterations} 轮）", cancellationToken => PhaseP.RunAsync(options, cancellationToken));
        runner.Register("q", "Data gRPC / SignalR / 账号切换", cancellationToken => PhaseQ.RunAsync(options, cancellationToken));
        runner.Register("r", $"Data 并发混沌与停机（{options.Operations} 操作）", cancellationToken => PhaseR.RunAsync(options, cancellationToken));
    }

    public static int PrintUsage()
    {
        Console.WriteLine("用法：dotnet run --project fixtures/AtomUI.City.Fixtures.StressCli -c Release -f <net8.0|net10.0> -- <command> [options]");
        Console.WriteLine("Localization commands: localization | localization-soak | localization-providers | localization-races | localization-chaos | localization-lifecycle | localization-suite | localization-extreme");
        Console.WriteLine("Data commands: data-workflow | data-realtime | data-chaos | data-suite | data-extreme");
        Console.WriteLine("Options: --profile <quick|standard|extreme> --seed <int> --operations <n> --workers <n> --data-iterations <n> --timeout <seconds>");
        return 2;
    }
}
