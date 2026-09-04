using AtomUI.City.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.CoreEventBus.DogfoodApp.Services;

public interface ISystemClock { DateTimeOffset UtcNow { get; } }
public interface IIdentifierFactory { string Create(string prefix); }
public interface IIdentityValidator { bool IsAllowed(string userId); }
public interface IWorkspaceCatalog { bool Exists(string workspaceId); }
public interface IJobRepository { void Store(string jobId); bool Contains(string jobId); }
public interface IJobPlanner { string Plan(string workspaceId, int sequence); }
public interface IExecutionEngine { string Execute(string jobId); }
public interface IAuditSink { void Write(string entry); int Count { get; } }
public interface IReportBuilder { string Build(string jobId); }
public interface INotificationGateway { void Send(string message); int Count { get; } }

[Service(ServiceLifetime.Singleton), ExposeServices(typeof(ISystemClock))]
public sealed class SystemClock : ISystemClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

[Service(ServiceLifetime.Singleton), ExposeServices(typeof(IIdentifierFactory))]
public sealed class IdentifierFactory(ISystemClock clock) : IIdentifierFactory
{
    private long _sequence;
    public string Create(string prefix) => $"{prefix}-{clock.UtcNow.ToUnixTimeSeconds()}-{Interlocked.Increment(ref _sequence)}";
}

[Service(ServiceLifetime.Singleton), ExposeServices(typeof(IIdentityValidator))]
public sealed class IdentityValidator(ISystemClock clock) : IIdentityValidator
{
    public bool IsAllowed(string userId) => !string.IsNullOrWhiteSpace(userId) && clock.UtcNow != default;
}

[Service(ServiceLifetime.Singleton), ExposeServices(typeof(IWorkspaceCatalog))]
public sealed class WorkspaceCatalog(IIdentityValidator identity) : IWorkspaceCatalog
{
    public bool Exists(string workspaceId) => identity.IsAllowed("system") && !string.IsNullOrWhiteSpace(workspaceId);
}

[Service(ServiceLifetime.Singleton), ExposeServices(typeof(IJobRepository))]
public sealed class JobRepository(ISystemClock clock) : IJobRepository
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> _jobs = new();
    public void Store(string jobId) => _jobs[jobId] = clock.UtcNow;
    public bool Contains(string jobId) => _jobs.ContainsKey(jobId);
}

[Service(ServiceLifetime.Singleton), ExposeServices(typeof(IJobPlanner))]
public sealed class JobPlanner(IWorkspaceCatalog workspaces) : IJobPlanner
{
    public string Plan(string workspaceId, int sequence) => workspaces.Exists(workspaceId)
        ? $"{workspaceId}/plan/{sequence}"
        : throw new InvalidOperationException("Unknown workspace.");
}

[Service(ServiceLifetime.Singleton), ExposeServices(typeof(IExecutionEngine))]
public sealed class ExecutionEngine(IJobPlanner planner) : IExecutionEngine
{
    public string Execute(string jobId) => $"executed:{planner.Plan("execution", jobId.Length)}";
}

[Service(ServiceLifetime.Singleton), ExposeServices(typeof(IAuditSink))]
public sealed class AuditSink(ISystemClock clock) : IAuditSink
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _entries = new();
    public int Count => _entries.Count;
    public void Write(string entry) => _entries.Enqueue($"{clock.UtcNow:O}:{entry}");
}

[Service(ServiceLifetime.Singleton), ExposeServices(typeof(IReportBuilder))]
public sealed class ReportBuilder(IAuditSink audit) : IReportBuilder
{
    public string Build(string jobId) => $"report:{jobId}:audit={audit.Count}";
}

[Service(ServiceLifetime.Singleton), ExposeServices(typeof(INotificationGateway))]
public sealed class NotificationGateway(IReportBuilder reports) : INotificationGateway
{
    private int _count;
    public int Count => Volatile.Read(ref _count);
    public void Send(string message)
    {
        _ = reports.Build(message);
        Interlocked.Increment(ref _count);
    }
}

public interface IUserSession { string UserId { get; } }
public interface IWorkspaceSession { string WorkspaceId { get; } }
public interface IJobSession { Guid InstanceId { get; } }
public interface IExecutionSession { Guid InstanceId { get; } }
public interface IAuditSession { Guid InstanceId { get; } }
public interface IReportSession { Guid InstanceId { get; } }
public interface INotificationSession { Guid InstanceId { get; } }
public interface IMaintenanceSession { Guid InstanceId { get; } }
public interface ICorrelationSession { string CorrelationId { get; } }
public interface IUnitOfWork { Guid InstanceId { get; } }

[ScopedService(typeof(IUserSession))]
public sealed class UserSession(IIdentityValidator identity) : IUserSession
{
    public string UserId { get; } = identity.IsAllowed("dogfood-user") ? "dogfood-user" : throw new InvalidOperationException();
}

[ScopedService(typeof(IWorkspaceSession))]
public sealed class WorkspaceSession(IWorkspaceCatalog catalog) : IWorkspaceSession
{
    public string WorkspaceId { get; } = catalog.Exists("workspace-main") ? "workspace-main" : throw new InvalidOperationException();
}

[ScopedService(typeof(IJobSession))] public sealed class JobSession(IJobRepository repository) : IJobSession { public Guid InstanceId { get; } = repository is not null ? Guid.NewGuid() : Guid.Empty; }
[ScopedService(typeof(IExecutionSession))] public sealed class ExecutionSession(IExecutionEngine engine) : IExecutionSession { public Guid InstanceId { get; } = engine is not null ? Guid.NewGuid() : Guid.Empty; }
[ScopedService(typeof(IAuditSession))] public sealed class AuditSession(IAuditSink sink) : IAuditSession { public Guid InstanceId { get; } = sink is not null ? Guid.NewGuid() : Guid.Empty; }
[ScopedService(typeof(IReportSession))] public sealed class ReportSession(IReportBuilder builder) : IReportSession { public Guid InstanceId { get; } = builder is not null ? Guid.NewGuid() : Guid.Empty; }
[ScopedService(typeof(INotificationSession))] public sealed class NotificationSession(INotificationGateway gateway) : INotificationSession { public Guid InstanceId { get; } = gateway is not null ? Guid.NewGuid() : Guid.Empty; }
[ScopedService(typeof(IMaintenanceSession))] public sealed class MaintenanceSession(ISystemClock clock) : IMaintenanceSession { public Guid InstanceId { get; } = clock.UtcNow != default ? Guid.NewGuid() : Guid.Empty; }
[ScopedService(typeof(ICorrelationSession))] public sealed class CorrelationSession(IIdentifierFactory identifiers) : ICorrelationSession { public string CorrelationId { get; } = identifiers.Create("correlation"); }
[ScopedService(typeof(IUnitOfWork))] public sealed class UnitOfWork(IJobRepository repository) : IUnitOfWork { public Guid InstanceId { get; } = repository is not null ? Guid.NewGuid() : Guid.Empty; }

public interface ISubmissionPolicy { bool Accept(int priority); }
public interface IWorkspacePolicy { bool Accept(string workspaceId); }
public interface IJobValidator { bool Validate(string jobId); }
public interface IExecutionPolicy { int Normalize(int workUnits); }
public interface IAuditFormatter { string Format(string value); }
public interface IReportFormatter { string Format(string value); }
public interface INotificationFormatter { string Format(string value); }
public interface IMaintenancePolicy { bool ShouldRun(int completed); }
public interface IRetryPolicy { int Attempts { get; } }
public interface IWorkflowCoordinator { string Prepare(string workspaceId, int sequence); }

[Service(ServiceLifetime.Transient), ExposeServices(typeof(ISubmissionPolicy))] public sealed class SubmissionPolicy : ISubmissionPolicy { public bool Accept(int priority) => priority is >= 0 and <= 10; }
[Service(ServiceLifetime.Transient), ExposeServices(typeof(IWorkspacePolicy))] public sealed class WorkspacePolicy(IWorkspaceCatalog catalog) : IWorkspacePolicy { public bool Accept(string workspaceId) => catalog.Exists(workspaceId); }
[Service(ServiceLifetime.Transient), ExposeServices(typeof(IJobValidator))] public sealed class JobValidator(IJobRepository repository) : IJobValidator { public bool Validate(string jobId) => repository.Contains(jobId); }
[Service(ServiceLifetime.Transient), ExposeServices(typeof(IExecutionPolicy))] public sealed class ExecutionPolicy : IExecutionPolicy { public int Normalize(int workUnits) => Math.Clamp(workUnits, 1, 100); }
[Service(ServiceLifetime.Transient), ExposeServices(typeof(IAuditFormatter))] public sealed class AuditFormatter : IAuditFormatter { public string Format(string value) => $"audit:{value}"; }
[Service(ServiceLifetime.Transient), ExposeServices(typeof(IReportFormatter))] public sealed class ReportFormatter : IReportFormatter { public string Format(string value) => $"report:{value}"; }
[Service(ServiceLifetime.Transient), ExposeServices(typeof(INotificationFormatter))] public sealed class NotificationFormatter : INotificationFormatter { public string Format(string value) => $"notification:{value}"; }
[Service(ServiceLifetime.Transient), ExposeServices(typeof(IMaintenancePolicy))] public sealed class MaintenancePolicy : IMaintenancePolicy { public bool ShouldRun(int completed) => completed > 0; }
[Service(ServiceLifetime.Transient), ExposeServices(typeof(IRetryPolicy))] public sealed class RetryPolicy : IRetryPolicy { public int Attempts => 3; }
[Service(ServiceLifetime.Transient), ExposeServices(typeof(IWorkflowCoordinator))]
public sealed class WorkflowCoordinator(IJobPlanner planner, ISubmissionPolicy submissions, IWorkspacePolicy workspaces) : IWorkflowCoordinator
{
    public string Prepare(string workspaceId, int sequence) => submissions.Accept(sequence % 10) && workspaces.Accept(workspaceId)
        ? planner.Plan(workspaceId, sequence)
        : throw new InvalidOperationException("Workflow rejected.");
}
