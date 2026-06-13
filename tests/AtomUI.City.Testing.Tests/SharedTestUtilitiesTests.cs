using AtomUI.City.Testing;

namespace AtomUI.City.Testing.Tests;

public sealed class SharedTestUtilitiesTests
{
    [Fact]
    public void TestDirectoryCreatesAndCleansUniqueDirectory()
    {
        string rootPath;

        using (var directory = TestDirectory.Create("shared-utilities"))
        {
            rootPath = directory.RootPath;
            File.WriteAllText(directory.GetPath("nested", "file.txt"), "content");

            Assert.True(Directory.Exists(rootPath));
            Assert.True(File.Exists(Path.Combine(rootPath, "nested", "file.txt")));
        }

        Assert.False(Directory.Exists(rootPath));
    }

    [Fact]
    public async Task DisposableTrackerDisposesResourcesInReverseOrder()
    {
        var calls = new List<string>();
        var tracker = new DisposableTracker();

        tracker.Track(new DelegateDisposable(() => calls.Add("first")));
        tracker.Track(new DelegateAsyncDisposable(() =>
        {
            calls.Add("second");
            return ValueTask.CompletedTask;
        }));

        await tracker.DisposeAsync();

        Assert.Equal(["second", "first"], calls);
    }

    [Fact]
    public void TestDiagnosticsCollectsEntriesInOrder()
    {
        var diagnostics = new TestDiagnostics();

        diagnostics.Add("CITY1001", "first");
        diagnostics.Add("CITY1002", "second", TestLayer.FrameworkIntegration);

        Assert.Collection(
            diagnostics.Entries,
            entry =>
            {
                Assert.Equal("CITY1001", entry.Code);
                Assert.Null(entry.Layer);
            },
            entry =>
            {
                Assert.Equal("CITY1002", entry.Code);
                Assert.Equal(TestLayer.FrameworkIntegration, entry.Layer);
            });
        Assert.True(diagnostics.Contains("CITY1002"));
    }

    [Fact]
    public void TestDiagnosticsEntriesRejectExternalMutation()
    {
        var diagnostics = new TestDiagnostics();

        diagnostics.Add("CITY1001", "first");
        var entries = Assert.IsAssignableFrom<IList<TestDiagnosticEntry>>(diagnostics.Entries);

        Assert.Throws<NotSupportedException>(() => entries.Add(new TestDiagnosticEntry("CITY1002", "second")));
        Assert.Single(diagnostics.Entries);
        Assert.True(diagnostics.Contains("CITY1001"));
        Assert.False(diagnostics.Contains("CITY1002"));
    }

    [Fact]
    public void DeterministicSchedulerRunsSameDueTimeWorkInScheduleOrder()
    {
        var scheduler = new DeterministicScheduler();
        var calls = new List<int>();

        for (var index = 0; index < 20; index++)
        {
            var capturedIndex = index;
            scheduler.Schedule(TimeSpan.FromSeconds(1), () => calls.Add(capturedIndex));
        }

        scheduler.AdvanceBy(TimeSpan.FromSeconds(1));

        Assert.Equal(Enumerable.Range(0, 20), calls);
    }

    [Fact]
    public void DeterministicSchedulerRecordsExceptionsAndContinuesRemainingWork()
    {
        var diagnostics = new TestDiagnostics();
        var scheduler = new DeterministicScheduler(diagnostics);
        var calls = new List<string>();

        var failedWork = scheduler.Schedule(TimeSpan.Zero, () => throw new InvalidOperationException("boom"));
        scheduler.Schedule(TimeSpan.Zero, () => calls.Add("after"));

        scheduler.RunDueWork();

        Assert.True(failedWork.IsFaulted);
        Assert.Equal("boom", failedWork.Exception?.Message);
        Assert.True(diagnostics.Contains("AUCTEST201"));
        Assert.Equal(["after"], calls);
    }

    [Fact]
    public void DeterministicSchedulerSkipsCanceledWork()
    {
        var scheduler = new DeterministicScheduler();
        var wasCalled = false;

        var work = scheduler.Schedule(TimeSpan.Zero, () => wasCalled = true);
        work.Cancel();
        scheduler.RunDueWork();

        Assert.False(wasCalled);
        Assert.True(work.IsCanceled);
        Assert.True(work.IsCompleted);
        Assert.Equal(0, scheduler.ScheduledCount);
    }

    [Fact]
    public void DeterministicSchedulerRejectsScheduleAfterDispose()
    {
        var scheduler = new DeterministicScheduler();

        scheduler.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scheduler.Schedule(TimeSpan.Zero, () => { }));
    }

    private sealed class DelegateDisposable : IDisposable
    {
        private readonly Action _dispose;

        public DelegateDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            _dispose();
        }
    }

    private sealed class DelegateAsyncDisposable : IAsyncDisposable
    {
        private readonly Func<ValueTask> _disposeAsync;

        public DelegateAsyncDisposable(Func<ValueTask> disposeAsync)
        {
            _disposeAsync = disposeAsync;
        }

        public ValueTask DisposeAsync()
        {
            return _disposeAsync();
        }
    }
}
