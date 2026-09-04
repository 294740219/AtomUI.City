using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;
using AtomUI.City.EventBus;

namespace AtomUI.City.CoreEventBus.DogfoodApp.Modules;

[ApplicationModule]
[ServiceRegistrationOwner]
[DependsOn(typeof(EventBusModule))]
[DependsOn(typeof(IdentityModule))]
[DependsOn(typeof(WorkspaceModule))]
[DependsOn(typeof(JobsModule))]
[DependsOn(typeof(ExecutionModule))]
[DependsOn(typeof(AuditModule))]
[DependsOn(typeof(ReportingModule))]
[DependsOn(typeof(NotificationModule))]
[DependsOn(typeof(MaintenanceModule))]
public sealed class DogfoodApplicationModule : ModuleBase;

[Module("Dogfood.Identity")]
public sealed class IdentityModule : ModuleBase;

[Module("Dogfood.Workspace")]
[DependsOn(typeof(IdentityModule))]
public sealed class WorkspaceModule : ModuleBase;

[Module("Dogfood.Jobs")]
[DependsOn(typeof(WorkspaceModule))]
public sealed class JobsModule : ModuleBase;

[Module("Dogfood.Execution")]
[DependsOn(typeof(JobsModule))]
public sealed class ExecutionModule : ModuleBase;

[Module("Dogfood.Audit")]
[DependsOn(typeof(JobsModule))]
public sealed class AuditModule : ModuleBase;

[Module("Dogfood.Reporting")]
[DependsOn(typeof(JobsModule))]
[DependsOn(typeof(AuditModule))]
public sealed class ReportingModule : ModuleBase;

[Module("Dogfood.Notification")]
[DependsOn(typeof(ExecutionModule))]
public sealed class NotificationModule : ModuleBase;

[Module("Dogfood.Maintenance")]
[DependsOn(typeof(ReportingModule))]
[DependsOn(typeof(NotificationModule))]
public sealed class MaintenanceModule : ModuleBase;
