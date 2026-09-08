namespace AtomUI.City.Fixtures.StressCli.Infrastructure;

/// <summary>
/// 不变量判定与报告：每个不变量记录 PASS/FAIL，最终报告逐条输出并决定退出码。
/// </summary>
public sealed class FixtureReport
{
    private readonly List<(string Id, string Description, bool Passed, string? Detail)> _results = [];
    private readonly object _gate = new();

    public int Passed => _results.Count(r => r.Passed);

    public int Failed => _results.Count(r => !r.Passed);

    public bool AllPassed => _results.Count > 0 && _results.All(r => r.Passed);

    public void Record(string id, string description, bool passed, string? detail = null)
    {
        lock (_gate)
        {
            _results.Add((id, description, passed, detail));
        }

        Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {id} {description}{(detail is null ? string.Empty : $" —— {detail}")}");
    }

    public void PrintSummary(TextWriter writer)
    {
        writer.WriteLine();
        writer.WriteLine("=== 不变量判定汇总 ===");

        foreach (var (id, description, passed, detail) in _results)
        {
            writer.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {id} {description}{(detail is null ? string.Empty : $" —— {detail}")}");
        }

        writer.WriteLine($"=== 总计：{Passed}/{_results.Count} 通过，{Failed} 失败 ===");
    }
}
