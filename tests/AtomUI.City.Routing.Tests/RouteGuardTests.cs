using AtomUI.City.Routing;

namespace AtomUI.City.Routing.Tests;

public sealed class RouteGuardTests
{
    [Fact]
    public async Task EnterGuardRejectsNavigationAndKeepsCurrentSnapshot()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route(
                    "settings",
                    "settings",
                    typeof(SettingsViewModel),
                    enterGuardTypes: [typeof(RejectEnterGuard)]),
            ]);
        var scope = new NavigationScope(graph, ResolveGuard);

        var result = await scope.Router.NavigateByPathAsync("settings");

        Assert.Equal(NavigationResultStatus.Rejected, result.Status);
        Assert.Equal("settings-disabled", result.Error?.Code);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task LeaveGuardRejectsNavigationAndKeepsPreviousSnapshot()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route(
                    "home",
                    "home",
                    typeof(HomeViewModel),
                    leaveGuardTypes: [typeof(RejectLeaveGuard)]),
                Route("settings", "settings", typeof(SettingsViewModel)),
            ]);
        var scope = new NavigationScope(graph, ResolveGuard);

        var first = await scope.Router.NavigateByPathAsync("home");
        var second = await scope.Router.NavigateByPathAsync("settings");

        Assert.Equal(NavigationResultStatus.Success, first.Status);
        Assert.Equal(NavigationResultStatus.Rejected, second.Status);
        Assert.Equal("unsaved-changes", second.Error?.Code);
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task MatchPolicySkipsRejectedCandidateAndTriesNextRoute()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route(
                    "settings.disabled",
                    "settings",
                    typeof(DisabledSettingsViewModel),
                    matchPolicyTypes: [typeof(DisabledMatchPolicy)]),
                Route("settings.enabled", "settings", typeof(SettingsViewModel)),
            ]);
        var scope = new NavigationScope(graph, ResolveGuard);

        var result = await scope.Router.NavigateByPathAsync("settings");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("settings.enabled", result.Route.RouteId);
        Assert.Equal(typeof(SettingsViewModel), result.Route.ViewModelTarget?.ViewModelType);
    }

    [Fact]
    public async Task GuardExceptionReturnsFailedResultAndKeepsCurrentSnapshot()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route(
                    "settings",
                    "settings",
                    typeof(SettingsViewModel),
                    enterGuardTypes: [typeof(ThrowingEnterGuard)]),
            ]);
        var scope = new NavigationScope(graph, ResolveGuard);

        var result = await scope.Router.NavigateByPathAsync("settings");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        Assert.Equal("CITY-NAVIGATION-FAILED", result.Error?.Code);
        Assert.IsType<InvalidOperationException>(result.Error?.Exception);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task GuardsRunInHierarchyOrderForEnterAndLeave()
    {
        var events = new List<string>();
        var graph = RouteGraphSnapshot.Create(
            [
                Layout(
                    "shell",
                    typeof(ShellViewModel),
                    enterGuardTypes: [typeof(RecordingEnterGuard)],
                    leaveGuardTypes: [typeof(RecordingLeaveGuard)]),
                Route(
                    "home",
                    "home",
                    typeof(HomeViewModel),
                    parentRouteId: "shell",
                    enterGuardTypes: [typeof(RecordingEnterGuard)],
                    leaveGuardTypes: [typeof(RecordingLeaveGuard)]),
                Route(
                    "outside",
                    "outside",
                    typeof(SettingsViewModel),
                    enterGuardTypes: [typeof(RecordingEnterGuard)]),
            ]);
        var scope = new NavigationScope(graph, type => ResolveRecordingGuard(type, events));

        var first = await scope.Router.NavigateByPathAsync("home");
        var second = await scope.Router.NavigateByPathAsync("outside");

        Assert.Equal(NavigationResultStatus.Success, first.Status);
        Assert.Equal(NavigationResultStatus.Success, second.Status);
        Assert.Equal(
            [
                "enter:shell",
                "enter:home",
                "leave:home",
                "leave:shell",
                "enter:outside",
            ],
            events);
    }

    [Fact]
    public async Task EnterGuardRedirectRunsRedirectTargetAndCommitsIt()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route(
                    "admin",
                    "admin",
                    typeof(SettingsViewModel),
                    enterGuardTypes: [typeof(RedirectToLoginGuard)]),
                Route("login", "login", typeof(LoginViewModel)),
            ]);
        var scope = new NavigationScope(graph, ResolveGuard);

        var result = await scope.Router.NavigateByPathAsync("admin");

        Assert.Equal(NavigationResultStatus.Redirected, result.Status);
        Assert.Equal("login", result.Route.RouteId);
        Assert.Equal("login", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task RedirectLoopReturnsFailedResultWithoutChangingCurrentSnapshot()
    {
        var graph = RouteGraphSnapshot.Create(
            [
                Route(
                    "loop-a",
                    "loop-a",
                    typeof(SettingsViewModel),
                    enterGuardTypes: [typeof(RedirectToLoopBGuard)]),
                Route(
                    "loop-b",
                    "loop-b",
                    typeof(LoginViewModel),
                    enterGuardTypes: [typeof(RedirectToLoopAGuard)]),
            ]);
        var scope = new NavigationScope(graph, ResolveGuard);

        var result = await scope.Router.NavigateByPathAsync("loop-a");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        Assert.Equal("CITY-NAVIGATION-REDIRECT-LOOP", result.Error?.Code);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task CancelledGuardStopsRemainingGuardsAndKeepsCurrentSnapshot()
    {
        var events = new List<string>();
        var graph = RouteGraphSnapshot.Create(
            [
                Route(
                    "settings",
                    "settings",
                    typeof(SettingsViewModel),
                    enterGuardTypes: [typeof(CancelEnterGuard), typeof(RecordingEnterGuard)]),
            ]);
        var scope = new NavigationScope(graph, type =>
            type == typeof(CancelEnterGuard)
                ? new CancelEnterGuard()
                : ResolveRecordingGuard(type, events));

        var result = await scope.Router.NavigateByPathAsync("settings");

        Assert.Equal(NavigationResultStatus.Cancelled, result.Status);
        Assert.Empty(events);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    private static object ResolveGuard(Type type)
    {
        if (type == typeof(RejectEnterGuard))
        {
            return new RejectEnterGuard();
        }

        if (type == typeof(RejectLeaveGuard))
        {
            return new RejectLeaveGuard();
        }

        if (type == typeof(DisabledMatchPolicy))
        {
            return new DisabledMatchPolicy();
        }

        if (type == typeof(ThrowingEnterGuard))
        {
            return new ThrowingEnterGuard();
        }

        if (type == typeof(RedirectToLoginGuard))
        {
            return new RedirectToLoginGuard();
        }

        if (type == typeof(RedirectToLoopBGuard))
        {
            return new RedirectToLoopBGuard();
        }

        if (type == typeof(RedirectToLoopAGuard))
        {
            return new RedirectToLoopAGuard();
        }

        throw new InvalidOperationException($"Unsupported guard type '{type.FullName}'.");
    }

    private static object ResolveRecordingGuard(Type type, List<string> events)
    {
        if (type == typeof(RecordingEnterGuard))
        {
            return new RecordingEnterGuard(events);
        }

        if (type == typeof(RecordingLeaveGuard))
        {
            return new RecordingLeaveGuard(events);
        }

        throw new InvalidOperationException($"Unsupported guard type '{type.FullName}'.");
    }

    private static RouteDescriptor Layout(
        string id,
        Type viewModelType,
        IReadOnlyList<Type>? enterGuardTypes = null,
        IReadOnlyList<Type>? leaveGuardTypes = null)
    {
        return new RouteDescriptor(
            id,
            RouteDefinitionKind.Layout,
            template: null,
            new ViewModelTargetDescriptor(viewModelType),
            parentRouteId: null,
            enterGuardTypes: enterGuardTypes,
            leaveGuardTypes: leaveGuardTypes);
    }

    private static RouteDescriptor Route(
        string id,
        string template,
        Type viewModelType,
        string? parentRouteId = null,
        IReadOnlyList<Type>? enterGuardTypes = null,
        IReadOnlyList<Type>? leaveGuardTypes = null,
        IReadOnlyList<Type>? matchPolicyTypes = null)
    {
        return new RouteDescriptor(
            id,
            RouteDefinitionKind.Route,
            template,
            new ViewModelTargetDescriptor(viewModelType),
            parentRouteId,
            enterGuardTypes: enterGuardTypes,
            leaveGuardTypes: leaveGuardTypes,
            matchPolicyTypes: matchPolicyTypes);
    }

    private sealed class RejectEnterGuard : IRouteEnterGuard
    {
        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(RouteGuardResult.Reject("settings-disabled"));
        }
    }

    private sealed class RejectLeaveGuard : IRouteLeaveGuard
    {
        public ValueTask<RouteGuardResult> CanLeaveAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(RouteGuardResult.Reject("unsaved-changes"));
        }
    }

    private sealed class DisabledMatchPolicy : IRouteMatchPolicy
    {
        public ValueTask<bool> CanMatchAsync(
            RouteMatchPolicyContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(false);
        }
    }

    private sealed class ThrowingEnterGuard : IRouteEnterGuard
    {
        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Guard failed.");
        }
    }

    private sealed class RedirectToLoginGuard : IRouteEnterGuard
    {
        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(
                RouteGuardResult.Redirect(
                    NavigationTarget.FromRouteReference("login", parameters: null, NavigationOptions.Default)));
        }
    }

    private sealed class RedirectToLoopBGuard : IRouteEnterGuard
    {
        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(
                RouteGuardResult.Redirect(
                    NavigationTarget.FromRouteReference("loop-b", parameters: null, NavigationOptions.Default)));
        }
    }

    private sealed class RedirectToLoopAGuard : IRouteEnterGuard
    {
        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(
                RouteGuardResult.Redirect(
                    NavigationTarget.FromRouteReference("loop-a", parameters: null, NavigationOptions.Default)));
        }
    }

    private sealed class CancelEnterGuard : IRouteEnterGuard
    {
        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(RouteGuardResult.Cancel());
        }
    }

    private sealed class RecordingEnterGuard(List<string> events) : IRouteEnterGuard
    {
        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            events.Add("enter:" + context.Route.RouteId);

            return ValueTask.FromResult(RouteGuardResult.Allow());
        }
    }

    private sealed class RecordingLeaveGuard(List<string> events) : IRouteLeaveGuard
    {
        public ValueTask<RouteGuardResult> CanLeaveAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            events.Add("leave:" + context.Route.RouteId);

            return ValueTask.FromResult(RouteGuardResult.Allow());
        }
    }

    private sealed class ShellViewModel;

    private sealed class HomeViewModel;

    private sealed class SettingsViewModel;

    private sealed class LoginViewModel;

    private sealed class DisabledSettingsViewModel;
}
