using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Tests;

public sealed class HostDiagnosticsTests
{
    [Fact]
    public void HostDiagnosticIdsIncludePhaseOneFailureCodes()
    {
        Assert.Equal("AUCHOST001", HostDiagnosticIds.HostBuilt);
        Assert.Equal("AUCHOST002", HostDiagnosticIds.HostStarted);
        Assert.Equal("AUCHOST003", HostDiagnosticIds.HostStopped);
        Assert.Equal("AUCHOST101", HostDiagnosticIds.HostBuildFailed);
        Assert.Equal("AUCHOST102", HostDiagnosticIds.HostStartFailed);
        Assert.Equal("AUCHOST103", HostDiagnosticIds.HostStopFailed);
        Assert.Equal("AUCHOST104", HostDiagnosticIds.LifecycleScopeCleanupFailed);
        Assert.Equal("AUCHOST105", HostDiagnosticIds.ModuleGraphFailed);
        Assert.Equal("AUCHOST106", HostDiagnosticIds.ModuleLifecycleFailed);
        Assert.Equal("AUCHOST107", HostDiagnosticIds.DispatcherUnavailable);
    }

    [Fact]
    public void DiagnosticContextRejectsExternalMutation()
    {
        var context = new Dictionary<string, string?>
        {
            ["moduleId"] = "SampleModule",
        };

        var record = new HostDiagnosticRecord(
            HostDiagnosticIds.ModuleLifecycleFailed,
            "Module failed.",
            HostDiagnosticSeverity.Error)
        {
            Context = context,
        };

        context["moduleId"] = "Changed";

        Assert.Equal("SampleModule", record.Context["moduleId"]);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IDictionary<string, string?>>(record.Context)["moduleId"] = "ChangedAgain");
    }

    [Fact]
    public async Task ApplicationHostRegistersDiagnosticsAndRecordsHostLifecycleEvents()
    {
        await using var host = ApplicationHost.CreateBuilder().Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();

        Assert.Contains(diagnostics.Records, record => record.Code == HostDiagnosticIds.HostBuilt);

        await host.StartAsync();
        await host.StopAsync();

        Assert.Contains(diagnostics.Records, record => record.Code == HostDiagnosticIds.HostStarted);
        Assert.Contains(diagnostics.Records, record => record.Code == HostDiagnosticIds.HostStopped);
    }

    [Fact]
    public void InMemoryDiagnosticsStoresRecordsInWriteOrder()
    {
        var diagnostics = new InMemoryHostDiagnostics();

        diagnostics.Write(new HostDiagnosticRecord("TEST001", "First", HostDiagnosticSeverity.Info));
        diagnostics.Write(new HostDiagnosticRecord("TEST002", "Second", HostDiagnosticSeverity.Warning));

        Assert.Equal(["TEST001", "TEST002"], diagnostics.Records.Select(record => record.Code));
    }

    [Fact]
    public void InMemoryDiagnosticsRecordsSnapshotRejectsExternalListMutation()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        diagnostics.Write(new HostDiagnosticRecord("TEST001", "First", HostDiagnosticSeverity.Info));
        var records = Assert.IsAssignableFrom<IList<HostDiagnosticRecord>>(diagnostics.Records);

        Assert.Throws<NotSupportedException>(() => records[0] = new HostDiagnosticRecord(
            "TEST999",
            "Changed",
            HostDiagnosticSeverity.Error));
        Assert.Equal("TEST001", diagnostics.Records[0].Code);
    }

    [Fact]
    public void BoundedDiagnosticsRetainNewestRecordsAndTrackDrops()
    {
        var diagnostics = new InMemoryHostDiagnostics(capacity: 2);

        diagnostics.Write(new HostDiagnosticRecord("A", "first", HostDiagnosticSeverity.Info));
        diagnostics.Write(new HostDiagnosticRecord("B", "second", HostDiagnosticSeverity.Info));
        diagnostics.Write(new HostDiagnosticRecord("C", "third", HostDiagnosticSeverity.Info));

        Assert.Equal(2, diagnostics.Capacity);
        Assert.Equal(1, diagnostics.DroppedCount);
        Assert.Equal(["B", "C"], diagnostics.Records.Select(record => record.Code));
    }

    [Fact]
    public void BuildFailureCanBeInspectedFromBuilderDiagnostics()
    {
        var builder = ApplicationHost.CreateBuilder();
        var diagnostics = builder.GetBuildDiagnostics();
        builder.UseModule<MissingDependencyModule>();

        Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostBuildFailed &&
            record.Context["stage"] == "ModuleGraph");
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.ModuleGraphFailed);
    }

    [DependsOn(typeof(UnregisteredModule))]
    private sealed class MissingDependencyModule : ModuleBase;

    private sealed class UnregisteredModule : ModuleBase;
}
