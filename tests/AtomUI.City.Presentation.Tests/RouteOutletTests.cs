using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Presentation;
using AtomUI.City.Core.Threading;

namespace AtomUI.City.Presentation.Tests;

public sealed class RouteOutletTests
{
    [Fact]
    public async Task OutletCommitsPrimaryContentOnUiDispatcher()
    {
        var dispatcher = new RecordingDispatcher();
        var outlet = new RouteOutlet("primary", dispatcher);
        var handle = BoundViewHandle.FromExisting(
            new SettingsView(),
            new SettingsViewModel());

        var result = await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", handle));

        Assert.True(result.Succeeded);
        Assert.Same(handle.View, outlet.CurrentContent);
        Assert.Equal(1, dispatcher.InvokeCount);
    }

    [Fact]
    public async Task OutletReplaceDisposesPreviousContent()
    {
        var dispatcher = new RecordingDispatcher();
        var outlet = new RouteOutlet("primary", dispatcher);
        var first = BoundViewHandle.FromExisting(new SettingsView(), new SettingsViewModel());
        var second = BoundViewHandle.FromExisting(new SettingsView(), new SettingsViewModel());

        await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", first));
        await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", second));

        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
        Assert.Same(second.View, outlet.CurrentContent);
    }

    [Fact]
    public async Task OutletRepeatCommitOfCurrentHandleIsNoOp()
    {
        var dispatcher = new RecordingDispatcher();
        var outlet = new RouteOutlet("primary", dispatcher);
        var disposeCount = 0;
        var handle = BoundViewHandle.FromExisting(
            new SettingsView(),
            new SettingsViewModel(),
            () => disposeCount++);

        await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", handle));
        var result = await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", handle));

        Assert.True(result.Succeeded);
        Assert.False(handle.IsDisposed);
        Assert.Equal(0, disposeCount);
        Assert.Same(handle.View, outlet.CurrentContent);
    }

    [Fact]
    public async Task OutletRollsBackWhenPreviousHandleDisposeFails()
    {
        var dispatcher = new RecordingDispatcher();
        var outlet = new RouteOutlet("primary", dispatcher);
        var previousView = new SettingsView();
        var previous = BoundViewHandle.FromExisting(
            previousView,
            new SettingsViewModel(),
            () => throw new InvalidOperationException("old dispose rejected"));
        var nextDisposeCount = 0;
        var next = BoundViewHandle.FromExisting(
            new SettingsView(),
            new SettingsViewModel(),
            () => nextDisposeCount++);

        await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", previous));
        var result = await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", next));

        Assert.False(result.Succeeded);
        Assert.Equal(PresentationError.OutletCommitFailed, result.Error);
        Assert.Same(previousView, outlet.CurrentContent);
        Assert.False(previous.IsDisposed);
        Assert.True(next.IsDisposed);
        Assert.Equal(1, nextDisposeCount);
    }

    [Fact]
    public async Task OutletClearDisposesCurrentContent()
    {
        var dispatcher = new RecordingDispatcher();
        var outlet = new RouteOutlet("primary", dispatcher);
        var first = BoundViewHandle.FromExisting(new SettingsView(), new SettingsViewModel());

        await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", first));
        var result = await outlet.CommitAsync(RouteOutletCommitPlan.Clear("primary"));

        Assert.True(result.Succeeded);
        Assert.True(first.IsDisposed);
        Assert.Null(outlet.CurrentContent);
    }

    [Fact]
    public async Task OutletCanceledBeforeAttachKeepsPreviousContentAndDisposesRejectedHandle()
    {
        var dispatcher = new RecordingDispatcher();
        var outlet = new RouteOutlet("primary", dispatcher);
        var previous = BoundViewHandle.FromExisting(new SettingsView(), new SettingsViewModel());
        var rejected = BoundViewHandle.FromExisting(new SettingsView(), new SettingsViewModel());
        using var cancellation = new CancellationTokenSource();

        await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", previous));
        await cancellation.CancelAsync();
        var result = await outlet.CommitAsync(
            RouteOutletCommitPlan.Replace("primary", rejected),
            cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Same(previous.View, outlet.CurrentContent);
        Assert.False(previous.IsDisposed);
        Assert.True(rejected.IsDisposed);
    }

    [Fact]
    public async Task OutletFailureKeepsPreviousContentAndDisposesRejectedHandle()
    {
        var dispatcher = new RecordingDispatcher();
        var outlet = new RouteOutlet("primary", dispatcher);
        var first = BoundViewHandle.FromExisting(new SettingsView(), new SettingsViewModel());
        var rejected = BoundViewHandle.FromExisting(new SettingsView(), new SettingsViewModel());

        await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", first));

        var result = await outlet.CommitAsync(RouteOutletCommitPlan.Replace("secondary", rejected));

        Assert.False(result.Succeeded);
        Assert.Equal(PresentationError.OutletNotFound, result.Error);
        Assert.Same(first.View, outlet.CurrentContent);
        Assert.True(rejected.IsDisposed);
    }

    [Fact]
    public async Task OutletRecordsCommitPlanAndSuccessDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var dispatcher = new RecordingDispatcher();
        var outlet = new RouteOutlet("primary", dispatcher, diagnostics);
        var handle = BoundViewHandle.FromExisting(
            new SettingsView(),
            new SettingsViewModel());

        await outlet.CommitAsync(RouteOutletCommitPlan.Replace("primary", handle));

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.OutletCommitPlanned &&
                record.Severity == HostDiagnosticSeverity.Info &&
                record.Message.Contains("primary", StringComparison.Ordinal) &&
                record.Message.Contains(nameof(RouteOutletOperation.Replace), StringComparison.Ordinal) &&
                record.Context["outletName"] == "primary" &&
                record.Context["operation"] == nameof(RouteOutletOperation.Replace));
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.OutletCommitSucceeded &&
                record.Severity == HostDiagnosticSeverity.Info &&
                record.Message.Contains("primary", StringComparison.Ordinal) &&
                record.Context["newViewType"] == typeof(SettingsView).FullName);
    }

    [Fact]
    public async Task OutletRecordsCommitPlanAndFailureDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var dispatcher = new RecordingDispatcher();
        var outlet = new RouteOutlet("primary", dispatcher, diagnostics);
        var rejected = BoundViewHandle.FromExisting(new SettingsView(), new SettingsViewModel());

        await outlet.CommitAsync(RouteOutletCommitPlan.Replace("secondary", rejected));

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.OutletCommitPlanned &&
                record.Severity == HostDiagnosticSeverity.Info &&
                record.Message.Contains("secondary", StringComparison.Ordinal));
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.OutletCommitFailed &&
                record.Severity == HostDiagnosticSeverity.Error &&
                record.Message.Contains(nameof(PresentationError.OutletNotFound), StringComparison.Ordinal) &&
                record.Context["error"] == nameof(PresentationError.OutletNotFound));
    }

    private sealed class RecordingDispatcher : IUiDispatcher
    {
        public int InvokeCount { get; private set; }

        public bool CheckAccess() => true;

        public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InvokeCount++;
            callback();

            return ValueTask.CompletedTask;
        }

        public ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InvokeCount++;

            return ValueTask.FromResult(callback());
        }

        public ValueTask PostAsync(
            Func<CancellationToken, ValueTask> callback,
            CancellationToken cancellationToken = default)
        {
            return callback(cancellationToken);
        }
    }

    private sealed class SettingsViewModel;

    private sealed class SettingsView;
}
