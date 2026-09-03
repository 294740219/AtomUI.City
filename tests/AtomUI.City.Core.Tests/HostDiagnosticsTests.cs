using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Tests;

public sealed class HostDiagnosticsTests
{
    [Fact]
    public void HostDiagnosticIdsMatchDocumentedCatalog()
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
        Assert.Equal("AUCHOST108", HostDiagnosticIds.LifecycleMiddlewareFailed);
        Assert.Equal("AUCHOST109", HostDiagnosticIds.HostBuildCleanupFailed);
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
    public void DiagnosticRecordRejectsInvalidRequiredValuesAtEveryInitializationPath()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HostDiagnosticRecord(null!, "Message", HostDiagnosticSeverity.Info));
        Assert.Throws<ArgumentException>(() =>
            new HostDiagnosticRecord("   ", "Message", HostDiagnosticSeverity.Info));
        Assert.Throws<ArgumentNullException>(() =>
            new HostDiagnosticRecord("TEST001", null!, HostDiagnosticSeverity.Info));
        Assert.Throws<ArgumentException>(() =>
            new HostDiagnosticRecord("TEST001", "   ", HostDiagnosticSeverity.Info));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HostDiagnosticRecord("TEST001", "Message", (HostDiagnosticSeverity)int.MaxValue));

        var valid = new HostDiagnosticRecord("TEST001", "Message", HostDiagnosticSeverity.Info);

        Assert.Throws<ArgumentException>(() => valid with { Code = "" });
        Assert.Throws<ArgumentException>(() => valid with { Message = "\t" });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            valid with { Severity = (HostDiagnosticSeverity)(-1) });
        Assert.Throws<ArgumentException>(() =>
            new HostDiagnosticRecord(
                "TEST001",
                "Message",
                HostDiagnosticSeverity.Info,
                Stage: (LifecycleStage?)default(LifecycleStage)));
        Assert.Throws<ArgumentException>(() =>
            valid with { Stage = default(LifecycleStage) });
    }

    [Fact]
    public async Task ApplicationHostRegistersDiagnosticsAndRecordsHostLifecycleEvents()
    {
        await using var host = ApplicationHostTestBuilder.Create().Build();
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
    public void CompletedDiagnosticsRejectWritesAndRetainReadableSnapshots()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        diagnostics.Write(new HostDiagnosticRecord("TEST001", "Before completion", HostDiagnosticSeverity.Info));

        diagnostics.Complete();
        diagnostics.Complete();
        diagnostics.Dispose();

        Assert.Equal(["TEST001"], diagnostics.Records.Select(record => record.Code));
        Assert.Throws<ObjectDisposedException>(() => diagnostics.Write(
            new HostDiagnosticRecord("TEST002", "After completion", HostDiagnosticSeverity.Info)));
    }

    [Fact]
    public async Task CompleteAndConcurrentWritersHaveOneAtomicBoundary()
    {
        const int writerCount = 512;
        var diagnostics = new InMemoryHostDiagnostics();
        using var start = new ManualResetEventSlim();
        var accepted = 0;
        var rejected = 0;

        var writers = Enumerable.Range(0, writerCount)
            .Select(index => Task.Run(() =>
            {
                start.Wait();

                try
                {
                    diagnostics.Write(new HostDiagnosticRecord(
                        $"TEST{index:D3}",
                        "Concurrent write",
                        HostDiagnosticSeverity.Info));
                    Interlocked.Increment(ref accepted);
                }
                catch (ObjectDisposedException)
                {
                    Interlocked.Increment(ref rejected);
                }
            }))
            .ToArray();
        var completers = Enumerable.Range(0, 32)
            .Select(index => Task.Run(() =>
            {
                start.Wait();
                if (index % 2 == 0)
                {
                    diagnostics.Complete();
                }
                else
                {
                    diagnostics.Dispose();
                }
            }))
            .ToArray();

        start.Set();
        await Task.WhenAll(writers.Concat(completers));

        Assert.Equal(writerCount, accepted + rejected);
        Assert.Equal(accepted, diagnostics.Records.Count);
        Assert.Throws<ObjectDisposedException>(() => diagnostics.Write(
            new HostDiagnosticRecord("FINAL", "Final write", HostDiagnosticSeverity.Info)));
    }

    [Fact]
    public async Task HostDisposalCompletesDiagnosticsAfterItsFinalRecord()
    {
        var host = ApplicationHostTestBuilder.Create().Build();
        var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();

        await host.DisposeAsync();

        Assert.Contains(diagnostics.Records, record => record.Code == HostDiagnosticIds.HostStopped);
        Assert.Throws<ObjectDisposedException>(() => diagnostics.Write(
            new HostDiagnosticRecord("TEST001", "After Host disposal", HostDiagnosticSeverity.Info)));
    }

    [Fact]
    public async Task DiagnosticsCompletionFailureDoesNotInterruptHostCleanup()
    {
        var diagnostics = new CompletionThrowingDiagnostics();
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureServices(services => services.AddSingleton<IHostDiagnostics>(diagnostics));
        var host = builder.Build();

        await host.DisposeAsync();
        await host.DisposeAsync();

        Assert.Equal(1, diagnostics.CompleteCount);
        Assert.Contains(diagnostics.Records, record => record.Code == HostDiagnosticIds.HostStopped);
    }

    [Fact]
    public void BuildFailureCanBeInspectedFromBuilderDiagnostics()
    {
        var builder = ApplicationHostTestBuilder.Create();
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

    private sealed class CompletionThrowingDiagnostics : IHostDiagnostics
    {
        private readonly InMemoryHostDiagnostics _inner = new();

        public int CompleteCount { get; private set; }

        public IReadOnlyList<HostDiagnosticRecord> Records => _inner.Records;

        public void Write(HostDiagnosticRecord record)
        {
            _inner.Write(record);
        }

        public void Complete()
        {
            CompleteCount++;
            throw new InvalidOperationException("Diagnostics completion failed.");
        }
    }
}
