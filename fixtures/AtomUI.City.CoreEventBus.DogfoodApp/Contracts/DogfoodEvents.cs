using AtomUI.City.CoreEventBus.DogfoodApp.Modules;
using AtomUI.City.EventBus;

namespace AtomUI.City.CoreEventBus.DogfoodApp.Contracts;

[EventContract("dogfood.identity.user-authenticated.v1", typeof(IdentityModule))]
public sealed record UserAuthenticated(string UserId);

[EventContract("dogfood.workspace.opened.v1", typeof(WorkspaceModule))]
public sealed record WorkspaceOpened(string WorkspaceId, string UserId);

[EventContract("dogfood.job.submitted.v1", typeof(JobsModule))]
[EventChannel("commands", Capacity = 64)]
public sealed record JobSubmitted(string WorkspaceId, string JobId, int Sequence);

[EventContract("dogfood.job.accepted.v1", typeof(JobsModule))]
public sealed record JobAccepted(string WorkspaceId, string JobId);

[EventContract("dogfood.job.queued.v1", typeof(JobsModule))]
public sealed record JobQueued(string WorkspaceId, string JobId);

[EventContract("dogfood.execution.started.v1", typeof(ExecutionModule))]
public sealed record ExecutionStarted(string WorkspaceId, string JobId);

[EventContract("dogfood.execution.progressed.v1", typeof(ExecutionModule))]
[EventChannel("jobs", Capacity = 512, ExecutionMode = EventChannelExecutionMode.Partitioned, MaximumConcurrency = 8)]
public sealed record JobProgressed(string WorkspaceId, string JobId, int Sequence);

[EventContract("dogfood.execution.completed.v1", typeof(ExecutionModule))]
public sealed record JobCompleted(string WorkspaceId, string JobId, bool Succeeded);

[EventContract("dogfood.audit.recorded.v1", typeof(AuditModule))]
public sealed record AuditRecorded(string JobId, string Action);

[EventContract("dogfood.report.generated.v1", typeof(ReportingModule))]
public sealed record ReportGenerated(string JobId, string ReportId);

[EventContract("dogfood.notification.sent.v1", typeof(NotificationModule))]
[EventChannel("telemetry", Capacity = 256, ExecutionMode = EventChannelExecutionMode.Concurrent, MaximumConcurrency = 8)]
public sealed record NotificationSent(string JobId, string Destination);

[EventContract("dogfood.maintenance.cycle.v1", typeof(MaintenanceModule))]
[EventChannel("failures", Capacity = 32)]
public sealed record MaintenanceCycle(string CycleId, int CompletedJobs);
