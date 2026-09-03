using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Core.Mvp.Foundation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.MvpCli;

[ApplicationModule]
[ServiceRegistrationOwner]
[DependsOn(typeof(FoundationModule))]
public sealed class MvpApplicationModule : ModuleBase;

[Service(ServiceLifetime.Singleton)]
public sealed class MvpApplicationInfo
{
    public string Name => "AtomUI.City.Core.MvpCli";
}

public sealed class MvpApplicationSession : IScopedDependency
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class MvpApplicationCommand : ITransientDependency
{
    public Guid Id { get; } = Guid.NewGuid();
}

public interface IStartupObserver
{
    Guid Id { get; }
}

[Service(ServiceLifetime.Singleton)]
[ExposeServices(typeof(IStartupObserver))]
public sealed class StartupObserver : IStartupObserver
{
    public Guid Id { get; } = Guid.NewGuid();
}

internal sealed class UserDiagnosticPolicy : AtomUI.City.Core.Mvp.Diagnostics.IDiagnosticPolicy
{
    public string Name => "user";
}
