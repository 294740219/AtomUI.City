using AtomUI.City.Fixtures.StressCli.Infrastructure;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>
/// 阶段调度器：Phase A-O 按施工波次逐个注册，run-all 依序执行；
/// 未施工的阶段访问时明确报告"未施工"，不静默通过。
/// </summary>
public sealed class PhaseRunner
{
    private readonly FixtureReport _report;
    private readonly LifecycleLedger _ledger;
    private readonly TimeSpan _phaseTimeout;
    private readonly Dictionary<string, (string Title, Func<CancellationToken, Task> Run)> _phases =
        new(StringComparer.OrdinalIgnoreCase);

    public PhaseRunner(FixtureReport report, LifecycleLedger ledger, TimeSpan phaseTimeout)
    {
        _report = report;
        _ledger = ledger;
        _phaseTimeout = phaseTimeout;
    }

    public void Register(string key, string title, Func<CancellationToken, Task> run)
    {
        _phases[key] = (title, run);
    }

    public IReadOnlyList<string> RegisteredKeys => [.. _phases.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase)];

    public async Task<int> RunAllAsync(CancellationToken cancellationToken = default)
    {
        return await RunPhasesAsync(RegisteredKeys, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> RunPhasesAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            var exit = await RunPhaseAsync(key, cancellationToken).ConfigureAwait(false);
            if (exit != 0)
            {
                return exit;
            }
        }

        return _report.AllPassed ? 0 : 3;
    }

    public async Task<int> RunPhaseAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_phases.TryGetValue(key, out var phase))
        {
            Console.Error.WriteLine($"阶段 '{key}' 尚未施工。已施工：{string.Join(", ", RegisteredKeys)}");
            return 2;
        }

        Console.WriteLine();
        Console.WriteLine($"=== Phase {key.ToUpperInvariant()} —— {phase.Title} ===");
        var failedBefore = _report.Failed;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_phaseTimeout);
            await phase.Run(timeout.Token)
                .WaitAsync(_phaseTimeout + TimeSpan.FromSeconds(5), cancellationToken)
                .ConfigureAwait(false);
            return _report.Failed == failedBefore ? 0 : 3;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var message = $"阶段超过 {_phaseTimeout} 或内部 watchdog 取消。";
            Console.Error.WriteLine($"阶段 {key} 超时：{message}");
            _report.Record($"PHASE-{key}", "阶段在时限内完成", false, message);
            return 1;
        }
        catch (TimeoutException exception)
        {
            Console.Error.WriteLine($"阶段 {key} 超时：{exception.Message}");
            _report.Record($"PHASE-{key}", "阶段在时限内完成", false, exception.Message);
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"阶段 {key} 异常终止：{exception}");
            _report.Record($"PHASE-{key}", "阶段执行完成", false, exception.Message);
            _report.PrintSummary(Console.Out);
            return 1;
        }
        finally
        {
            _report.PrintSummary(Console.Out);
        }
    }
}
