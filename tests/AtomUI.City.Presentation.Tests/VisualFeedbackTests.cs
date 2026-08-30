using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Presentation;

namespace AtomUI.City.Presentation.Tests;

public sealed class VisualFeedbackTests
{
    [Fact]
    public void VisualLifecycleHubPublishesNormalizedVisualTreeEvents()
    {
        var hub = new VisualLifecycleHub();
        var events = new List<VisualLifecycleEvent>();
        var view = new SettingsView();
        using var subscription = hub.Subscribe(events.Add);

        hub.Notify(view, VisualLifecycleEventKind.Attached);
        hub.Notify(view, VisualLifecycleEventKind.Detached);

        Assert.Collection(
            events,
            item =>
            {
                Assert.Same(view, item.View);
                Assert.Equal(VisualLifecycleEventKind.Attached, item.Kind);
            },
            item =>
            {
                Assert.Same(view, item.View);
                Assert.Equal(VisualLifecycleEventKind.Detached, item.Kind);
            });
    }

    [Fact]
    public void DisposedVisualLifecycleSubscriptionStopsReceivingEvents()
    {
        var hub = new VisualLifecycleHub();
        var events = new List<VisualLifecycleEvent>();
        using var subscription = hub.Subscribe(events.Add);

        subscription.Dispose();
        hub.Notify(new SettingsView(), VisualLifecycleEventKind.Attached);

        Assert.Empty(events);
    }

    [Fact]
    public void VisualLifecycleHubPublishesFocusAndVisibilityEventsInOrder()
    {
        var hub = new VisualLifecycleHub();
        var events = new List<VisualLifecycleEvent>();
        var view = new SettingsView();
        using var subscription = hub.Subscribe(events.Add);

        var focused = Enum.Parse<VisualLifecycleEventKind>("Focused");
        var visible = Enum.Parse<VisualLifecycleEventKind>("Visible");
        var hidden = Enum.Parse<VisualLifecycleEventKind>("Hidden");
        var unfocused = Enum.Parse<VisualLifecycleEventKind>("Unfocused");

        hub.Notify(view, focused);
        hub.Notify(view, visible);
        hub.Notify(view, hidden);
        hub.Notify(view, unfocused);

        Assert.Collection(
            events,
            item => Assert.Equal(focused, item.Kind),
            item => Assert.Equal(visible, item.Kind),
            item => Assert.Equal(hidden, item.Kind),
            item => Assert.Equal(unfocused, item.Kind));
    }

    [Fact]
    public void VisualLifecycleHubIsolatesHandlerFailuresAndContinuesPublishing()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var hub = new VisualLifecycleHub(diagnostics);
        var view = new SettingsView();
        var events = new List<string>();
        using var first = hub.Subscribe(_ => events.Add("first"));
        using var failing = hub.Subscribe(_ => throw new InvalidOperationException("adapter failed"));
        using var second = hub.Subscribe(_ => events.Add("second"));

        hub.Notify(view, VisualLifecycleEventKind.Detached);

        Assert.Equal(["first", "second"], events);
        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.VisualLifecycleAdapterFailed &&
                record.Severity == HostDiagnosticSeverity.Error &&
                record.Context["viewType"] == typeof(SettingsView).FullName &&
                record.Context["eventKind"] == nameof(VisualLifecycleEventKind.Detached) &&
                record.Context["error"] == typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void VisualLifecycleHubRecordsAdapterExecutionDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var hub = new VisualLifecycleHub(diagnostics);
        var view = new SettingsView();
        using var subscription = hub.Subscribe(_ => { });

        hub.Notify(view, VisualLifecycleEventKind.Attached);

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.VisualLifecycleAdapterExecuted &&
                record.Severity == HostDiagnosticSeverity.Info &&
                record.Message.Contains(typeof(SettingsView).FullName!, StringComparison.Ordinal) &&
                record.Message.Contains(nameof(VisualLifecycleEventKind.Attached), StringComparison.Ordinal) &&
                record.Context["viewType"] == typeof(SettingsView).FullName &&
                record.Context["eventKind"] == nameof(VisualLifecycleEventKind.Attached));
    }

    [Fact]
    public void VisualLifecycleHubRecordsTargetViewModelDiagnosticsWhenAvailable()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var hub = new VisualLifecycleHub(diagnostics);
        var view = new DataContextView
        {
            DataContext = new SettingsViewModel(),
        };
        using var subscription = hub.Subscribe(_ => { });

        hub.Notify(view, VisualLifecycleEventKind.Attached);

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.VisualLifecycleAdapterExecuted &&
                record.Context["viewType"] == typeof(DataContextView).FullName &&
                record.Context["viewModelType"] == typeof(SettingsViewModel).FullName);
    }

    [Fact]
    public void VisualLifecycleHubRecordsAdapterFailureDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var hub = new VisualLifecycleHub(diagnostics);
        var view = new SettingsView();
        using var subscription = hub.Subscribe(_ => throw new InvalidOperationException("adapter failed"));

        hub.Notify(view, VisualLifecycleEventKind.Detached);

        Assert.Contains(
            diagnostics.Records,
            record =>
                record.Code == PresentationDiagnosticIds.VisualLifecycleAdapterFailed &&
                record.Severity == HostDiagnosticSeverity.Error &&
                record.Message.Contains("adapter failed", StringComparison.Ordinal) &&
                record.Message.Contains(nameof(VisualLifecycleEventKind.Detached), StringComparison.Ordinal) &&
                record.Context["viewType"] == typeof(SettingsView).FullName &&
                record.Context["eventKind"] == nameof(VisualLifecycleEventKind.Detached) &&
                record.Context["error"] == typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void UiStateFeedbackPolicyOnlyAllowsSemanticFeedbackToViewModel()
    {
        Assert.True(UiStateFeedbackPolicy.CanNotifyViewModel(UiStateFeedbackKind.ValueChanged));
        Assert.True(UiStateFeedbackPolicy.CanNotifyViewModel(UiStateFeedbackKind.CommandInvoked));
        Assert.True(UiStateFeedbackPolicy.CanNotifyViewModel(UiStateFeedbackKind.InteractionCompleted));
        Assert.True(UiStateFeedbackPolicy.CanNotifyViewModel(UiStateFeedbackKind.ValidationRequested));
        Assert.True(UiStateFeedbackPolicy.CanNotifyViewModel(UiStateFeedbackKind.SelectionChanged));

        Assert.False(UiStateFeedbackPolicy.CanNotifyViewModel(UiStateFeedbackKind.HoverChanged));
        Assert.False(UiStateFeedbackPolicy.CanNotifyViewModel(UiStateFeedbackKind.PointerMoved));
        Assert.False(UiStateFeedbackPolicy.CanNotifyViewModel(UiStateFeedbackKind.LayoutUpdated));
        Assert.False(UiStateFeedbackPolicy.CanNotifyViewModel(UiStateFeedbackKind.ScrollOffsetChanged));
        Assert.False(UiStateFeedbackPolicy.CanNotifyViewModel(UiStateFeedbackKind.AnimationStateChanged));
    }

    private sealed class SettingsView;

    private sealed class DataContextView : IViewDataContextAware
    {
        public object? DataContext { get; set; }
    }

    private sealed class SettingsViewModel;
}
