using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

Summary[] summaries = BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args)
    .ToArray();

var reports = summaries.SelectMany(summary => summary.Reports).ToArray();
if (reports.Length == 0)
{
    Console.Error.WriteLine("EVENTBUS_BENCHMARK_GATE_FAILED: no benchmark cases were executed.");
    return 2;
}

var unsuccessful = reports.Where(report => !report.Success || report.ResultStatistics is null).ToArray();
if (unsuccessful.Length > 0)
{
    Console.Error.WriteLine(
        $"EVENTBUS_BENCHMARK_GATE_FAILED: {unsuccessful.Length} of {reports.Length} benchmark cases produced no valid result.");
    return 3;
}

Console.WriteLine($"EVENTBUS_BENCHMARK_GATE_OK cases={reports.Length}");
return 0;
