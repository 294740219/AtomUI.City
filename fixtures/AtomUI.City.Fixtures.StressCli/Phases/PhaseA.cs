using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Modules;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>
/// Phase A：40 个模块组成多层依赖图，验证
/// I1（启动顺序满足全部 DependsOn 成对约束）、
/// I2（停机顺序为启动关系的精确反向）、
/// I10（每模块七阶段里程碑各恰好一次，幂等无重复）。
/// </summary>
public static class PhaseA
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();

        var builder = StressHost.CreateBuilder();

        await using var host = builder.Build();

        await host.StartAsync(cancellationToken);

        var initSequence = SequenceOf(LifecycleMilestone.Initializing);

        var topologyFailures = new List<string>();

        foreach (var (module, dependsOn) in ModuleGraph.Edges)
        {
            if (!initSequence.TryGetValue(dependsOn, out var dependencySeq))
            {
                topologyFailures.Add($"{dependsOn} 从未初始化（被 {module} 依赖）");
                continue;
            }

            if (!initSequence.TryGetValue(module, out var moduleSeq))
            {
                topologyFailures.Add($"{module} 从未初始化（依赖 {dependsOn}）");
                continue;
            }

            if (dependencySeq >= moduleSeq)
            {
                topologyFailures.Add($"拓扑违反：{dependsOn}(#{dependencySeq}) 应先于 {module}(#{moduleSeq})");
            }
        }

        _report.Record(
            "I1",
            $"模块启动顺序满足全部 {ModuleGraph.Edges.Count} 条 DependsOn 成对约束",
            topologyFailures.Count == 0,
            topologyFailures.Count == 0 ? null : string.Join("; ", topologyFailures.Take(5)));

        await host.StopAsync(cancellationToken);

        var stopSequence = SequenceOf(LifecycleMilestone.Stopping);

        var shutdownFailures = new List<string>();

        foreach (var (module, dependsOn) in ModuleGraph.Edges)
        {
            if (!stopSequence.TryGetValue(module, out var moduleStopSeq) ||
                !stopSequence.TryGetValue(dependsOn, out var dependencyStopSeq))
            {
                shutdownFailures.Add($"{module} 或 {dependsOn} 缺少 Stopping 里程碑");
                continue;
            }

            if (moduleStopSeq >= dependencyStopSeq)
            {
                shutdownFailures.Add($"逆序违反：{module}(#{moduleStopSeq}) 应先于 {dependsOn}(#{dependencyStopSeq}) 停机");
            }
        }

        _report.Record(
            "I2",
            "模块停机顺序为启动依赖关系的精确反向",
            shutdownFailures.Count == 0,
            shutdownFailures.Count == 0 ? null : string.Join("; ", shutdownFailures.Take(5)));

        var presenceFailures = new List<string>();
        var duplicateFailures = new List<string>();
        var milestones = new[]
        {
            LifecycleMilestone.Configuring,
            LifecycleMilestone.Configured,
            LifecycleMilestone.PreInitializing,
            LifecycleMilestone.Initializing,
            LifecycleMilestone.PostInitializing,
            LifecycleMilestone.Stopping,
            LifecycleMilestone.Stopped,
        };

        foreach (var module in ModuleGraph.AllModules)
        {
            foreach (var milestone in milestones)
            {
                var count = FixtureState.Ledger.CountMilestone(module, milestone);
                if (count == 0)
                {
                    presenceFailures.Add($"{module} 缺少 {milestone}");
                }
                else if (count > 1)
                {
                    duplicateFailures.Add($"{module} 的 {milestone} 重复 {count} 次");
                }
            }
        }

        _report.Record(
            "I10-presence",
            "40 个模块 × 7 个生命周期里程碑全部出现",
            presenceFailures.Count == 0 && ModuleGraph.AllModules.Count == StressCliProgram.ModuleCount && ModuleGraph.Edges.Count == 78,
            presenceFailures.Count == 0
                ? $"modules={ModuleGraph.AllModules.Count} edges={ModuleGraph.Edges.Count}"
                : string.Join("; ", presenceFailures.Take(5)));

        _report.Record(
            "I10-idempotent",
            "生命周期里程碑无重复（幂等无重放）",
            duplicateFailures.Count == 0,
            duplicateFailures.Count == 0 ? null : string.Join("; ", duplicateFailures.Take(5)));
    }

    private static IReadOnlyDictionary<string, int> SequenceOf(LifecycleMilestone milestone)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        var sequence = 0;

        foreach (var entry in FixtureState.Ledger.Entries)
        {
            if (entry.Milestone == milestone)
            {
                map[entry.Module] = ++sequence;
            }
        }

        return map;
    }

    private static readonly FixtureReport _report = FixtureState.Report;
}
