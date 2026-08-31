using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Core.Tests;

public sealed class LifecycleScopeTreeTests
{
    [Fact]
    public async Task DisposedChildDetachesFromParentAndDoesNotBreakParentStop()
    {
        await using var root = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");
        var child = root.CreateChild(LifecycleScopeKind.Operation, "completed-operation");

        await child.DisposeAsync();
        await root.StopAsync();

        Assert.Empty(root.Children);
        Assert.Equal(LifecycleScopeState.Stopped, root.State);
    }

    [Fact]
    public void ScopeTreeModelsHostApplicationAndNavigationRuntimeBoundaries()
    {
        using var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");
        var application = host.CreateChild(LifecycleScopeKind.Application, "application");
        var navigation = application.CreateChild(LifecycleScopeKind.Navigation, "main-navigation");

        Assert.Null(host.Parent);
        Assert.Same(host, application.Parent);
        Assert.Same(application, navigation.Parent);
        Assert.Equal([application], host.Children);
        Assert.Equal([navigation], application.Children);
        Assert.Equal(LifecycleScopeState.Running, host.State);
        Assert.Equal(LifecycleScopeState.Running, navigation.State);
    }

    [Fact]
    public void ChildrenRejectExternalListMutation()
    {
        using var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");
        var application = host.CreateChild(LifecycleScopeKind.Application, "application");
        var children = Assert.IsAssignableFrom<IList<LifecycleScope>>(host.Children);

        Assert.Throws<NotSupportedException>(() => children.Add(application));
        Assert.Equal([application], host.Children);
    }

    [Fact]
    public async Task StoppingParentScopeStopsChildrenAndCancelsTokens()
    {
        await using var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");
        var application = host.CreateChild(LifecycleScopeKind.Application, "application");
        var navigation = application.CreateChild(LifecycleScopeKind.Navigation, "main-navigation");

        await host.StopAsync();

        Assert.True(host.CancellationToken.IsCancellationRequested);
        Assert.True(application.CancellationToken.IsCancellationRequested);
        Assert.True(navigation.CancellationToken.IsCancellationRequested);
        Assert.Equal(LifecycleScopeState.Stopped, host.State);
        Assert.Equal(LifecycleScopeState.Stopped, application.State);
        Assert.Equal(LifecycleScopeState.Stopped, navigation.State);
    }

    [Fact]
    public async Task StoppedScopeRejectsNewChildren()
    {
        await using var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");

        await host.StopAsync();

        Assert.Throws<InvalidOperationException>(() => host.CreateChild(LifecycleScopeKind.Application, "application"));
    }

    [Fact]
    public async Task DisposeStopsAndDisposesChildrenLeafFirst()
    {
        var order = new List<string>();
        await using var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");
        var application = host.CreateChild(LifecycleScopeKind.Application, "application");
        var operation = application.CreateChild(LifecycleScopeKind.Operation, "operation");

        operation.Disposed += (_, _) => order.Add("operation");
        application.Disposed += (_, _) => order.Add("application");
        host.Disposed += (_, _) => order.Add("host");

        await host.DisposeAsync();

        Assert.Equal(["operation", "application", "host"], order);
        Assert.Equal(LifecycleScopeState.Disposed, host.State);
        Assert.Equal(LifecycleScopeState.Disposed, application.State);
        Assert.Equal(LifecycleScopeState.Disposed, operation.State);
    }

    [Fact]
    public async Task ConcurrentStopAndCreateChildDoNotCorruptScopeState()
    {
        await using var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");

        var stopTask = host.StopAsync().AsTask();
        await stopTask;

        Assert.Throws<InvalidOperationException>(() =>
            host.CreateChild(LifecycleScopeKind.Operation, "late-operation"));
        Assert.Equal(LifecycleScopeState.Stopped, host.State);
    }

    [Fact]
    public async Task CancellationCallbackCanReadScopeStateFromAnotherThread()
    {
        var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");
        using var readRequested = new ManualResetEventSlim();
        var stateRead = new TaskCompletionSource<LifecycleScopeState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = Task.Run(() =>
        {
            readRequested.Wait();
            stateRead.SetResult(host.State);
        });
        var callbackObservedState = false;
        using var registration = host.CancellationToken.Register(() =>
        {
            readRequested.Set();
            callbackObservedState = stateRead.Task.Wait(TimeSpan.FromSeconds(2));
        });

        await host.StopAsync();
        await reader;

        Assert.True(callbackObservedState);
        Assert.Equal(LifecycleScopeState.Stopping, await stateRead.Task);
        await host.DisposeAsync();
    }

    [Fact]
    public async Task RecursiveStopFromCancellationCallbackFailsFast()
    {
        var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");
        Exception? recursiveFailure = null;
        using var registration = host.CancellationToken.Register(() =>
        {
            recursiveFailure = Record.Exception(() =>
            {
                host.StopAsync().AsTask().GetAwaiter().GetResult();
            });
        });

        await host.StopAsync();

        var failure = Assert.IsType<InvalidOperationException>(recursiveFailure);
        Assert.Contains("cannot be invoked recursively", failure.Message, StringComparison.Ordinal);
        Assert.Equal(LifecycleScopeState.Stopped, host.State);
        await host.DisposeAsync();
    }

    [Fact]
    public async Task RecursiveDisposeFromDisposedNotificationFailsFast()
    {
        var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");
        Exception? recursiveFailure = null;
        host.Disposed += (_, _) =>
        {
            recursiveFailure = Record.Exception(() =>
            {
                host.DisposeAsync();
            });
        };

        await host.DisposeAsync();

        var failure = Assert.IsType<InvalidOperationException>(recursiveFailure);
        Assert.Contains("cannot be invoked recursively", failure.Message, StringComparison.Ordinal);
        Assert.Equal(LifecycleScopeState.Disposed, host.State);
    }

    [Fact]
    public async Task DisposeIsIdempotent()
    {
        var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");

        await host.DisposeAsync();
        await host.DisposeAsync();
        host.Dispose();

        Assert.Equal(LifecycleScopeState.Disposed, host.State);
    }

    [Fact]
    public void ModuleAndPluginAreNotPublicScopeKinds()
    {
        var scopeKindNames = Enum.GetNames<LifecycleScopeKind>();

        Assert.DoesNotContain("Module", scopeKindNames);
        Assert.DoesNotContain("Plugin", scopeKindNames);
    }

    [Fact]
    public async Task StopContinuesAcrossChildFailuresAndRecordsDiagnostics()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host", diagnostics);
        var failing = host.CreateChild(LifecycleScopeKind.Operation, "failing");
        var healthy = host.CreateChild(LifecycleScopeKind.Operation, "healthy");
        using var registration = failing.CancellationToken.Register(
            static () => throw new InvalidOperationException("cancel failed"));

        await Assert.ThrowsAsync<AggregateException>(async () => await host.StopAsync());

        Assert.Equal(LifecycleScopeState.Faulted, host.State);
        Assert.Equal(LifecycleScopeState.Stopped, failing.State);
        Assert.Equal(LifecycleScopeState.Stopped, healthy.State);
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.LifecycleScopeCleanupFailed &&
            record.ScopeId == "host");

        await Assert.ThrowsAsync<AggregateException>(async () => await host.DisposeAsync());
        Assert.Equal(LifecycleScopeState.Disposed, host.State);
        Assert.Equal(LifecycleScopeState.Disposed, healthy.State);
    }
}
