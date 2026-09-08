using AtomUI.City.Fixtures.StressCli.Services;

namespace AtomUI.City.Fixtures.StressCli.Services;

// Reporting(2)

public interface IReportBuilder
{
    string Build(string title);
}

public sealed class ReportBuilderService : IReportBuilder
{
    public string Build(string title) => $"[report:{title}]";
}

public interface IReportFormatter
{
    string Format(string raw);
}

public sealed class ReportFormatterService : IReportFormatter
{
    public string Format(string raw) => raw.Trim();
}

// Analytics(2) —— 跨模块构造依赖：IMetricsCollector(Telemetry) + IReportBuilder(Reporting)

public interface IAnalyticsEngine
{
    IMetricsCollector Collector { get; }
    string Summarize();
}

public sealed class AnalyticsEngineService : IAnalyticsEngine
{
    private readonly IMetricsCollector _collector;
    private readonly IReportBuilder _reportBuilder;

    public AnalyticsEngineService(IMetricsCollector collector, IReportBuilder reportBuilder)
    {
        _collector = collector;
        _reportBuilder = reportBuilder;
    }

    public IMetricsCollector Collector => _collector;
    public string Summarize() => _reportBuilder.Build("analytics-summary");
}

public interface ITrendDetector
{
    bool Rising(decimal series);
}

public sealed class TrendDetectorService : ITrendDetector
{
    public bool Rising(decimal series) => series > 0;
}

// Notifications(1)

public interface INotificationDispatcher
{
    int Dispatched { get; }
    void Dispatch(string message);
}

public sealed class NotificationDispatcherService : INotificationDispatcher
{
    private int _dispatched;
    public int Dispatched => _dispatched;
    public void Dispatch(string message) => Interlocked.Increment(ref _dispatched);
}

// Audit(1)

public interface IAuditTrail
{
    int Entries { get; }
    void Append(string entry);
}

public sealed class AuditTrailService : IAuditTrail
{
    private readonly List<string> _entries = [];
    public int Entries => _entries.Count;
    public void Append(string entry) => _entries.Add(entry);
}

// Dashboard(1) —— 跨模块依赖 Analytics

public interface IDashboardAggregator
{
    IAnalyticsEngine Engine { get; }
}

public sealed class DashboardAggregatorService : IDashboardAggregator
{
    private readonly IAnalyticsEngine _engine;
    public DashboardAggregatorService(IAnalyticsEngine engine) => _engine = engine;
    public IAnalyticsEngine Engine => _engine;
}

// Workspace(1)

public interface IWorkspaceStore
{
    int Saved { get; }
    void Save(string workspace);
}

public sealed class WorkspaceStoreService : IWorkspaceStore
{
    private readonly List<string> _saved = [];
    public int Saved => _saved.Count;
    public void Save(string workspace) => _saved.Add(workspace);
}
