using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Core.Mvp.Data;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Mvp.Reporting;

[ServiceRegistrationOwner]
[DependsOn(typeof(DataModule))]
public sealed class ReportingModule : ModuleBase;

[ScopedService]
public sealed class ReportReader(IRecordReader records)
{
    public string Read() => records.Read();
}

[Service(ServiceLifetime.Transient)]
public sealed class ReportFormatter
{
    public string Format(string value) => $"report:{value}";
}

public interface IReportExporter
{
    string Format { get; }
}

[Service(ServiceLifetime.Singleton, Key = "json")]
[ExposeServices(typeof(IReportExporter))]
public sealed class JsonReportExporter : IReportExporter
{
    public string Format => "json";
}

[Service(ServiceLifetime.Singleton, Key = "text")]
[ExposeServices(typeof(IReportExporter))]
public sealed class TextReportExporter : IReportExporter
{
    public string Format => "text";
}
