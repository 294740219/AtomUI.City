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
    public async Task RouteReferenceRejectsMissingOrInvalidTemplateParameters()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route("profile", "profile/{id:int}", typeof(ProfileViewModel)),
            ]);
        var scope = new NavigationScope(graph);

        var missing = await scope.NavigateAsync(new RouteReference("profile"));
        var invalid = await scope.NavigateAsync(
            new RouteReference<ProfileParameters>(
                "profile",
                _ => new Dictionary<string, string> { ["id"] = "invalid" }),
            new ProfileParameters(0));

        Assert.Equal("CITY-NAVIGATION-PARAMETER-BINDING-FAILED", missing.Error?.Code);
        Assert.Equal("CITY-NAVIGATION-PARAMETER-BINDING-FAILED", invalid.Error?.Code);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task StaticRedirectNavigatesToConfiguredTarget()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                new RouteDescriptor(
                    "old-settings",
                    RouteDefinitionKind.Redirect,
                    "old-settings",
                    viewModelTarget: null,
                    redirectTargetRouteId: "settings"),
                Route("settings", "settings", typeof(SettingsViewModel)),
            ]);
        var scope = new NavigationScope(graph);

        var result = await scope.NavigateByPathAsync("old-settings");

        Assert.Equal(NavigationResultStatus.Redirected, result.Status);
        Assert.Equal("settings", result.Route.RouteId);
        Assert.Equal("settings", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task NavigationTimeoutCancelsRunningGuardWithoutCommit()
    {
        var guard = new BlockingEnterGuard();
        var graph = RouteGraphSnapshot.Create(
            [
                Route("slow", "slow", typeof(SettingsViewModel), enterGuardTypes: [typeof(BlockingEnterGuard)]),
            ]);
        var scope = ScopeWithBlockingGuard(graph, guard);

        var result = await scope.NavigateByPathAsync(
            "slow",
            new NavigationOptions { Timeout = TimeSpan.FromMilliseconds(50) });

        Assert.Equal(NavigationResultStatus.Cancelled, result.Status);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
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
    public async Task NewestCancelPreviousRequestSupersedesAlreadyQueuedReplacement()
    {
        var guard = new StubbornBlockingGuard();
        var graph = RouteGraphSnapshot.Create(
            [
                Route("slow", "slow", typeof(SettingsViewModel), enterGuardTypes: [typeof(StubbornBlockingGuard)]),
                Route("middle", "middle", typeof(ProfileViewModel)),
                Route("latest", "latest", typeof(ShellViewModel)),
            ]);
        var scope = new NavigationScope(graph, type => type == typeof(StubbornBlockingGuard) ? guard : null);
        var first = scope.NavigateByPathAsync("slow").AsTask();
        await guard.Entered.Task;

        var middle = scope.NavigateByPathAsync("middle").AsTask();
        var latest = scope.NavigateByPathAsync("latest").AsTask();
        guard.Allow.SetResult();

        var results = await Task.WhenAll(first, middle, latest);

        Assert.Equal(NavigationResultStatus.Cancelled, results[0].Status);
        Assert.Equal(NavigationResultStatus.Cancelled, results[1].Status);
        Assert.Equal(NavigationResultStatus.Success, results[2].Status);
        Assert.Equal("latest", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task ReentrantNavigationFromGuardIsRejectedInsteadOfDeadlocking()
    {
        var guard = new ReentrantEnterGuard();
        var graph = RouteGraphSnapshot.Create(
            [
                Route("guarded", "guarded", typeof(SettingsViewModel), enterGuardTypes: [typeof(ReentrantEnterGuard)]),
                Route("nested", "nested", typeof(ProfileViewModel)),
            ]);
        var scope = new NavigationScope(graph, type => type == typeof(ReentrantEnterGuard) ? guard : null);
        guard.Router = scope;

        var outer = await scope.NavigateByPathAsync("guarded").AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(NavigationResultStatus.Success, outer.Status);
        Assert.NotNull(guard.NestedResult);
        Assert.Equal(NavigationResultStatus.Rejected, guard.NestedResult.Status);
        Assert.Equal("CITY-NAVIGATION-REENTRANT", guard.NestedResult.Error?.Code);
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

    [Fact]
    public async Task DisposeAsyncWaitsForRunningUserCodeToExit()
    {
        var guard = new StubbornBlockingGuard();
        var graph = RouteGraphSnapshot.Create(
            [
                Route("slow", "slow", typeof(SettingsViewModel), enterGuardTypes: [typeof(StubbornBlockingGuard)]),
            ]);
        var scope = new NavigationScope(graph, type => type == typeof(StubbornBlockingGuard) ? guard : null);
        var running = scope.NavigateByPathAsync("slow").AsTask();
        await guard.Entered.Task;

        var disposing = scope.DisposeAsync().AsTask();
        Assert.NotSame(disposing, await Task.WhenAny(disposing, Task.Delay(100)));

        guard.Allow.SetResult();
        await disposing;

        Assert.Equal(NavigationResultStatus.Cancelled, (await running).Status);
    }

    [Fact]
    public async Task BackAndForwardNavigateThroughRecordedJournal()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route("home", "home", typeof(ShellViewModel)),
                Route("profile", "profile/{id:int}", typeof(ProfileViewModel)),
                Route("settings", "settings", typeof(SettingsViewModel)),
            ]);
        var scope = new NavigationScope(graph);

        await scope.Router.NavigateByPathAsync("home");
        await scope.Router.NavigateByPathAsync("profile/42");
        await scope.Router.NavigateByPathAsync("settings");

        var backToProfile = await scope.Router.BackAsync();
        var backToHome = await scope.Router.BackAsync();
        var forwardToProfile = await scope.Router.ForwardAsync();

        Assert.Equal(NavigationResultStatus.Success, backToProfile.Status);
        Assert.Equal("profile", backToProfile.Route.RouteId);
        Assert.Equal("42", backToProfile.Parameters["id"]);
        Assert.Equal(NavigationResultStatus.Success, backToHome.Status);
        Assert.Equal("home", backToHome.Route.RouteId);
        Assert.Equal(NavigationResultStatus.Success, forwardToProfile.Status);
        Assert.Equal("profile", forwardToProfile.Route.RouteId);
        Assert.Equal("profile", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task ConcurrentBackRequestsAreSerializedWithJournalMutation()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route("home", "home", typeof(ShellViewModel)),
                Route("profile", "profile", typeof(ProfileViewModel)),
                Route("settings", "settings", typeof(SettingsViewModel)),
            ]);
        var scope = new NavigationScope(graph);
        await scope.NavigateByPathAsync("home");
        await scope.NavigateByPathAsync("profile");
        await scope.NavigateByPathAsync("settings");

        var results = await Task.WhenAll(
            scope.BackAsync().AsTask(),
            scope.BackAsync().AsTask());

        Assert.All(results, result => Assert.Equal(NavigationResultStatus.Success, result.Status));
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task ReplaceCurrentDoesNotAddBackEntry()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route("home", "home", typeof(ShellViewModel)),
                Route("settings", "settings", typeof(SettingsViewModel)),
            ]);
        var scope = new NavigationScope(graph);

        await scope.Router.NavigateByPathAsync("home");
        var replacement = await scope.Router.NavigateByPathAsync(
            "settings",
            new NavigationOptions { Mode = NavigationMode.Replace });
        var back = await scope.Router.BackAsync();

        Assert.Equal(NavigationResultStatus.Success, replacement.Status);
        Assert.Equal(NavigationResultStatus.Rejected, back.Status);
        Assert.Equal("CITY-NAVIGATION-JOURNAL-NOT-AVAILABLE", back.Error?.Code);
        Assert.Equal("settings", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task FailedNavigationDoesNotWriteJournal()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route("home", "home", typeof(ShellViewModel)),
            ]);
        var scope = new NavigationScope(graph);

        await scope.Router.NavigateByPathAsync("home");
        var missing = await scope.Router.NavigateByPathAsync("missing");
        var back = await scope.Router.BackAsync();

        Assert.Equal(NavigationResultStatus.NotFound, missing.Status);
        Assert.Equal(NavigationResultStatus.Rejected, back.Status);
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task JournalCapacityTrimsOldEntriesAndSnapshotKeepsReuseKey()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route("home", "home", typeof(ShellViewModel)),
                Route("profile", "profile/{id:int}", typeof(ProfileViewModel), reuseKey: "profile:{id}"),
                Route("settings", "settings", typeof(SettingsViewModel)),
            ]);
        var scope = new NavigationScope(graph);
        var options = new NavigationOptions { JournalCapacity = 2 };

        await scope.Router.NavigateByPathAsync("home", options);
        await scope.Router.NavigateByPathAsync("profile/42", options);
        Assert.Equal("profile:{id}", scope.CurrentSnapshot.ReuseKey);
        await scope.Router.NavigateByPathAsync("settings", options);

        var backToProfile = await scope.Router.BackAsync();
        var trimmedBack = await scope.Router.BackAsync();

        Assert.Equal(NavigationResultStatus.Success, backToProfile.Status);
        Assert.Equal("profile", backToProfile.Route.RouteId);
        Assert.Equal(NavigationResultStatus.Rejected, trimmedBack.Status);
        Assert.Equal("profile", scope.CurrentSnapshot.Route.RouteId);
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
        IReadOnlyList<Type>? enterGuardTypes = null,
        string? reuseKey = null)
    {
        return new RouteDescriptor(
            id,
            RouteDefinitionKind.Route,
            template,
            new ViewModelTargetDescriptor(
                viewModelType,
                parameterBindings: null,
                reuseKey: reuseKey),
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

    private sealed class StubbornBlockingGuard : IRouteEnterGuard
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Allow { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Allow.Task;
            return RouteGuardResult.Allow();
        }
    }

    private sealed class ReentrantEnterGuard : IRouteEnterGuard
    {
        public IRouter? Router { get; set; }

        public NavigationResult? NestedResult { get; private set; }

        public async ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            NestedResult = await Router!.NavigateByPathAsync("nested", cancellationToken: CancellationToken.None);
            return RouteGuardResult.Allow();
        }
    }

    private sealed class ShellViewModel;

    private sealed class SettingsViewModel;

    private sealed class ProfileViewModel;

    private readonly record struct ProfileParameters(int Id);
}
