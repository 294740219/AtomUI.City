using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Core.Mvp.Foundation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Mvp.Diagnostics;

[ServiceRegistrationOwner]
[DependsOn(typeof(FoundationModule))]
public sealed class DiagnosticsModule : ModuleBase
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IDiagnosticPolicy, PreDiagnosticPolicy>();
    }
}

[Service(ServiceLifetime.Singleton)]
public sealed class AuditSink
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class MetricsScope : IScopedDependency
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class TraceFormatter : ITransientDependency
{
    public string Format(string value) => $"trace:{value}";
}

public interface IDiagnosticPolicy
{
    string Name { get; }
}

public sealed class PreDiagnosticPolicy : IDiagnosticPolicy
{
    public string Name => "pre";
}

[Service(ServiceLifetime.Singleton, Replace = true)]
[ExposeServices(typeof(IDiagnosticPolicy))]
public sealed class GeneratedDiagnosticPolicy : IDiagnosticPolicy
{
    public string Name => "generated-replace";
}
