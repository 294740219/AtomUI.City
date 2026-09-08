namespace AtomUI.City.Fixtures.StressCli.Infrastructure;

public enum LifecycleMilestone
{
    Configuring,
    Configured,
    PreInitializing,
    Initializing,
    PostInitializing,
    Started,
    Stopping,
    Stopped,
}

/// <summary>
/// 模块生命周期台账：记录每个模块的里程碑与全局序号，
/// 用于验证 I1（启动=拓扑序）、I2（停机=启动逆序）、I10（无重复里程碑）。
/// </summary>
public sealed class LifecycleLedger
{
    private readonly object _gate = new();
    private readonly List<(int Seq, string Module, LifecycleMilestone Milestone)> _entries = [];
    private int _seq;

    public void Record(string module, LifecycleMilestone milestone)
    {
        lock (_gate)
        {
            _entries.Add((++_seq, module, milestone));
        }
    }

    public IReadOnlyList<(int Seq, string Module, LifecycleMilestone Milestone)> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    /// <summary>按里程碑取模块出现顺序（去重，保持首次出现次序）。</summary>
    public IReadOnlyList<string> ModulesInMilestoneOrder(LifecycleMilestone milestone)
    {
        lock (_gate)
        {
            return _entries
                .Where(entry => entry.Milestone == milestone)
                .Select(entry => entry.Module)
                .Distinct()
                .ToArray();
        }
    }

    public int CountMilestone(string module, LifecycleMilestone milestone)
    {
        lock (_gate)
        {
            return _entries.Count(entry => entry.Module == module && entry.Milestone == milestone);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _seq = 0;
        }
    }
}
