using System.Globalization;

using AtomUI.City.Routing;

namespace AtomUI.City.Routing.Tests;

public sealed class NavigationScopeTests
{
    [Fact]
    public async Task NavigateByPathUpdatesCurrentSnapshot()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Layout("shell", typeof(ShellViewModel)),
                Route("settings", "settings", typeof(SettingsViewModel), parentRouteId: "shell"),
            ]);
        var scope = new NavigationScope(graph);

        var result = await scope.Router.NavigateByPathAsync("settings");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("settings", result.Route.RouteId);
        Assert.Equal("settings", scope.CurrentSnapshot.Route.RouteId);
        Assert.Equal(NavigationTargetKind.Path, result.Target.Kind);
    }

    [Fact]
    public async Task NavigateByRouteReferenceFindsRouteById()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Layout("shell", typeof(ShellViewModel)),
                Route("settings", "settings", typeof(SettingsViewModel), parentRouteId: "shell"),
            ]);
        var scope = new NavigationScope(graph);

        var result = await scope.Router.NavigateAsync(new RouteReference("settings"));

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("settings", result.Route.RouteId);
        Assert.Equal(NavigationTargetKind.RouteReference, result.Target.Kind);
        Assert.Equal("settings", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task NavigateByRouteReferenceBindsTypedParametersThroughRouteReferenceBinder()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Layout("shell", typeof(ShellViewModel)),
                Route("profile", "profile/{id:int}", typeof(ProfileViewModel), parentRouteId: "shell"),
            ]);
        var scope = new NavigationScope(graph);
        var route = new RouteReference<ProfileParameters>(
            "profile",
            parameters => new Dictionary<string, string>
            {
                ["id"] = parameters.Id.ToString(CultureInfo.InvariantCulture),
            });

        var result = await scope.Router.NavigateAsync(route, new ProfileParameters(42));

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("profile", result.Route.RouteId);
        Assert.Equal("42", result.Parameters["id"]);
        Assert.Equal("42", scope.CurrentSnapshot.Parameters["id"]);
    }

    [Fact]
    public async Task NavigateByPathReturnsNotFoundWithoutChangingCurrentSnapshot()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route("settings", "settings", typeof(SettingsViewModel)),
            ]);
        var scope = new NavigationScope(graph);

        var result = await scope.Router.NavigateByPathAsync("missing");

        Assert.Equal(NavigationResultStatus.NotFound, result.Status);
        Assert.Equal("CITY-NAVIGATION-NOT-FOUND", result.Error?.Code);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task CancelledNavigationReturnsCancelledResultWithoutChangingCurrentSnapshot()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route("settings", "settings", typeof(SettingsViewModel)),
            ]);
        var scope = new NavigationScope(graph);
        using var cancellationTokenSource = new CancellationTokenSource();

        await cancellationTokenSource.CancelAsync();

        var result = await scope.Router.NavigateByPathAsync(
            "settings",
            cancellationToken: cancellationTokenSource.Token);

        Assert.Equal(NavigationResultStatus.Cancelled, result.Status);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task RejectIfBusyReturnsRejectedWithoutWaitingForRunningNavigation()
    {
        var guard = new BlockingEnterGuard();
        var graph = RouteGraphSnapshot.Create(
            [
                Route(
                    "slow",
                    "slow",
                    typeof(SettingsViewModel),
                    enterGuardTypes: [typeof(BlockingEnterGuard)]),
                Route("fast", "fast", typeof(ProfileViewModel)),
            ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(BlockingEnterGuard)
                ? guard
                : throw new InvalidOperationException($"Unsupported service type '{type.FullName}'."));

        var running = scope.Router.NavigateByPathAsync("slow").AsTask();
        await guard.Entered.Task;

        var rejected = scope.Router
            .NavigateByPathAsync(
                "fast",
                new NavigationOptions { ConcurrencyPolicy = NavigationConcurrencyPolicy.RejectIfBusy })
            .AsTask();

        try
        {
            var completed = await Task.WhenAny(rejected, Task.Delay(TimeSpan.FromMilliseconds(250)));

            Assert.Same(rejected, completed);
            var result = await rejected;

            Assert.Equal(NavigationResultStatus.Rejected, result.Status);
            Assert.Equal("CITY-NAVIGATION-BUSY", result.Error?.Code);
            Assert.Null(scope.CurrentSnapshot.ActiveRoute);
        }
        finally
        {
            guard.Allow.SetResult();
            await running;
        }
    }

    [Fact]
    public async Task CancelPreviousCancelsRunningNavigationBeforeCommittingReplacement()
    {
        var guard = new BlockingEnterGuard();
        var graph = RouteGraphSnapshot.Create(
            [
                Route(
                    "slow",
                    "slow",
                    typeof(SettingsViewModel),
                    enterGuardTypes: [typeof(BlockingEnterGuard)]),
                Route("fast", "fast", typeof(ProfileViewModel)),
            ]);
        var scope = ScopeWithBlockingGuard(graph, guard);

        var running = scope.Router.NavigateByPathAsync("slow").AsTask();
        await guard.Entered.Task;

        var replacement = scope.Router.NavigateByPathAsync("fast").AsTask();

        var cancelled = await running;
        var committed = await replacement;

        Assert.Equal(NavigationResultStatus.Cancelled, cancelled.Status);
        Assert.Equal(NavigationResultStatus.Success, committed.Status);
        Assert.Equal("fast", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task QueueConcurrencyPolicyWaitsForRunningNavigationToComplete()
    {
        var guard = new BlockingEnterGuard();
        var graph = RouteGraphSnapshot.Create(
            [
                Route(
                    "slow",
                    "slow",
                    typeof(SettingsViewModel),
                    enterGuardTypes: [typeof(BlockingEnterGuard)]),
                Route("fast", "fast", typeof(ProfileViewModel)),
            ]);
        var scope = ScopeWithBlockingGuard(graph, guard);
        var options = new NavigationOptions { ConcurrencyPolicy = NavigationConcurrencyPolicy.Queue };

        var running = scope.Router.NavigateByPathAsync("slow", options).AsTask();
        await guard.Entered.Task;

        var queued = scope.Router.NavigateByPathAsync("fast", options).AsTask();

        try
        {
            var earlyCompletion = await Task.WhenAny(queued, Task.Delay(TimeSpan.FromMilliseconds(150)));

            Assert.NotSame(queued, earlyCompletion);

            guard.Allow.SetResult();

            var first = await running;
            var second = await queued;

            Assert.Equal(NavigationResultStatus.Success, first.Status);
            Assert.Equal(NavigationResultStatus.Success, second.Status);
            Assert.Equal("fast", scope.CurrentSnapshot.Route.RouteId);
        }
        finally
        {
            guard.Allow.TrySetResult();
        }
    }

    [Fact]
    public async Task DisposeCancelsRunningNavigationAndRejectsNewNavigation()
    {
        var guard = new BlockingEnterGuard();
        var graph = RouteGraphSnapshot.Create(
            [
                Route(
                    "slow",
                    "slow",
                    typeof(SettingsViewModel),
                    enterGuardTypes: [typeof(BlockingEnterGuard)]),
                Route("fast", "fast", typeof(ProfileViewModel)),
            ]);
        var scope = ScopeWithBlockingGuard(graph, guard);

        var running = scope.Router.NavigateByPathAsync("slow").AsTask();
        await guard.Entered.Task;

        await scope.DisposeAsync();
        await scope.DisposeAsync();
        scope.Dispose();

        var cancelled = await running;
        var rejected = await scope.Router.NavigateByPathAsync("fast");

        Assert.Equal(NavigationResultStatus.Cancelled, cancelled.Status);
        Assert.Equal(NavigationResultStatus.Rejected, rejected.Status);
        Assert.Equal("CITY-NAVIGATION-SCOPE-DISPOSED", rejected.Error?.Code);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    private static RouteDescriptor Layout(string id, Type viewModelType)
    {
        return new RouteDescriptor(
            id,
            RouteDefinitionKind.Layout,
            template: null,
            new ViewModelTargetDescriptor(viewModelType),
            parentRouteId: null);
    }

    private static RouteDescriptor Route(
        string id,
        string template,
        Type viewModelType,
        string? parentRouteId = null,
        IReadOnlyList<Type>? enterGuardTypes = null)
    {
        return new RouteDescriptor(
            id,
            RouteDefinitionKind.Route,
            template,
            new ViewModelTargetDescriptor(viewModelType),
            parentRouteId,
            enterGuardTypes: enterGuardTypes);
    }

    private static NavigationScope ScopeWithBlockingGuard(
        RouteGraphSnapshot graph,
        BlockingEnterGuard guard)
    {
        return new NavigationScope(
            graph,
            type => type == typeof(BlockingEnterGuard)
                ? guard
                : throw new InvalidOperationException($"Unsupported service type '{type.FullName}'."));
    }

    private sealed class BlockingEnterGuard : IRouteEnterGuard
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Allow { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Allow.Task.WaitAsync(cancellationToken);

            return RouteGuardResult.Allow();
        }
    }

    private sealed class ShellViewModel;

    private sealed class SettingsViewModel;

    private sealed class ProfileViewModel;

    private readonly record struct ProfileParameters(int Id);
}
