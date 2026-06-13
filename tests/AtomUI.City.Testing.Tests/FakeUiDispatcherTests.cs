using AtomUI.City.Testing;

namespace AtomUI.City.Testing.Tests;

public sealed class FakeUiDispatcherTests
{
    [Fact]
    public async Task ImplementsCoreDispatcherAndRunsInlineWhenAlreadyOnUiThread()
    {
        var dispatcher = new FakeUiDispatcher();
        var calls = new List<string>();

        Assert.IsAssignableFrom<AtomUI.City.Threading.IUiDispatcher>(dispatcher);
        Assert.False(dispatcher.CheckAccess());

        await dispatcher.InvokeAsync(() =>
        {
            Assert.True(dispatcher.CheckAccess());
            calls.Add("invoke");
        });
        await dispatcher.PostAsync(_ =>
        {
            calls.Add(dispatcher.CheckAccess() ? "post-ui" : "post-background");
            return ValueTask.CompletedTask;
        });

        Assert.Equal(["invoke"], calls);
        Assert.Equal(1, dispatcher.PendingCount);

        dispatcher.Drain();

        Assert.False(dispatcher.CheckAccess());
        Assert.Equal(["invoke", "post-ui"], calls);
    }

    [Fact]
    public void PostQueuesWorkUntilDrainIsCalled()
    {
        var dispatcher = new FakeUiDispatcher();
        var calls = new List<string>();

        dispatcher.Post(() => calls.Add("first"));
        dispatcher.Post(() => calls.Add("second"));

        Assert.Empty(calls);
        Assert.Equal(2, dispatcher.PendingCount);

        dispatcher.Drain();

        Assert.Equal(["first", "second"], calls);
        Assert.Equal(0, dispatcher.PendingCount);
    }

    [Fact]
    public void CanceledWorkItemDoesNotRunDuringDrain()
    {
        var dispatcher = new FakeUiDispatcher();
        var wasCalled = false;

        var workItem = dispatcher.Post(() => wasCalled = true);
        workItem.Cancel();
        dispatcher.Drain();

        Assert.False(wasCalled);
        Assert.True(workItem.IsCanceled);
    }

    [Fact]
    public void DrainRecordsWorkExceptionsAndContinuesRemainingWork()
    {
        var diagnostics = new TestDiagnostics();
        var dispatcher = new FakeUiDispatcher(diagnostics);
        var calls = new List<string>();

        dispatcher.Post(() => throw new InvalidOperationException("boom"));
        dispatcher.Post(() => calls.Add("after"));

        dispatcher.Drain();

        Assert.Equal(["after"], calls);
        Assert.Equal(0, dispatcher.PendingCount);
        Assert.True(diagnostics.Contains("AUCTEST101"));
    }
}
