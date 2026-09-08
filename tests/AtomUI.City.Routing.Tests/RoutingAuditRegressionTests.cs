using AtomUI.City.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Routing.Tests;

public sealed class RoutingAuditRegressionTests
{
    [Fact]
    public async Task MiddlewareFailureAfterNextDoesNotCommitPreparedNavigation()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            Route("home", "home"),
            Route("broken", "broken", middlewareTypes: [typeof(PostNextThrowingMiddleware)]),
        ]);
        var scope = new NavigationScope(graph, _ => new PostNextThrowingMiddleware());
        await scope.NavigateByPathAsync("home");

        var result = await scope.NavigateByPathAsync("broken");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task MiddlewareCannotTurnRepeatedNextInvocationIntoCommittedSuccess()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            Route("target", "target", middlewareTypes: [typeof(RepeatedNextMiddleware)]),
        ]);
        var scope = new NavigationScope(graph, _ => new RepeatedNextMiddleware());

        var result = await scope.NavigateByPathAsync("target");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        Assert.Equal("CITY-NAVIGATION-MIDDLEWARE-INVALID-RESULT", result.Error?.Code);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public void RepeatedAddRoutingCallsAggregateStaticRoutes()
    {
        var services = new ServiceCollection();
        services.AddRouting();
        services.AddRouting([Route("home", "home")]);
        using var provider = services.BuildServiceProvider();

        var graph = provider.GetRequiredService<IRouteGraphProvider>().CurrentSnapshot;

        Assert.Equal("home", Assert.Single(graph.Routes).RouteId);
    }

    [Fact]
    public async Task SharedParentResolversRunForEverySuccessfulNavigation()
    {
        var resolver = new CountingDataResolver();
        var graph = RouteGraphSnapshot.Create(
        [
            new RouteDescriptor(
                "shell",
                RouteDefinitionKind.Layout,
                null,
                new ViewModelTargetDescriptor(typeof(TestViewModel)),
                resolverTypes: [typeof(CountingDataResolver)]),
            Route("first", "first/{id:int}", parentRouteId: "shell"),
            Route("second", "second/{id:int}", parentRouteId: "shell"),
        ]);
        var scope = new NavigationScope(graph, type => type == typeof(CountingDataResolver) ? resolver : null);

        await scope.NavigateByPathAsync("first/1");
        await scope.NavigateByPathAsync("second/2");
        var result = await scope.NavigateByPathAsync("second/3");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal(3, resolver.InvocationCount);
        Assert.Equal(3, scope.CurrentSnapshot.ResolvedData["parent-generation"]);
    }

    [Fact]
    public async Task PreCancelledCancelPreviousRequestDoesNotCancelRunningNavigation()
    {
        var resolver = new ControlledResolver();
        var graph = RouteGraphSnapshot.Create(
        [
            Route("slow", "slow", resolverTypes: [typeof(ControlledResolver)]),
            Route("other", "other"),
        ]);
        var scope = new NavigationScope(graph, type => type == typeof(ControlledResolver) ? resolver : null);
        var running = scope.NavigateByPathAsync("slow").AsTask();
        await resolver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var cancelled = await scope.NavigateByPathAsync("other", cancellationToken: cancellation.Token);
        resolver.Release.TrySetResult();
        var completed = await running;

        Assert.Equal(NavigationResultStatus.Cancelled, cancelled.Status);
        Assert.Equal(NavigationResultStatus.Success, completed.Status);
        Assert.Equal("slow", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task InheritedExecutionContextExpiresAfterOwningNavigationCompletes()
    {
        var middleware = new DelayedNavigationMiddleware();
        var graph = RouteGraphSnapshot.Create(
        [
            Route("first", "first", middlewareTypes: [typeof(DelayedNavigationMiddleware)]),
            Route("second", "second"),
        ]);
        var scope = new NavigationScope(graph, type => type == typeof(DelayedNavigationMiddleware) ? middleware : null);
        middleware.Scope = scope;

        var first = await scope.NavigateByPathAsync("first");
        middleware.Release.TrySetResult();
        var delayed = await middleware.DelayedNavigation!.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(NavigationResultStatus.Success, first.Status);
        Assert.Equal(NavigationResultStatus.Success, delayed.Status);
        Assert.Equal("second", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task CancelledBackNavigationRestoresPoppedJournalEntry()
    {
        var guard = new CancelFirstLeaveGuard();
        var graph = RouteGraphSnapshot.Create(
        [
            Route("home", "home"),
            Route("profile", "profile", leaveGuardTypes: [typeof(CancelFirstLeaveGuard)]),
        ]);
        var scope = new NavigationScope(graph, type => type == typeof(CancelFirstLeaveGuard) ? guard : null);
        await scope.NavigateByPathAsync("home");
        await scope.NavigateByPathAsync("profile");
        using var cancellation = new CancellationTokenSource();
        var back = scope.BackAsync(cancellation.Token).AsTask();
        await guard.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var cancelled = await back;
        var retried = await scope.BackAsync();

        Assert.Equal(NavigationResultStatus.Cancelled, cancelled.Status);
        Assert.Equal(NavigationResultStatus.Success, retried.Status);
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task StaticRedirectPreservesDeepLinkQueryAndFragment()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            new RouteDescriptor(
                "legacy",
                RouteDefinitionKind.Redirect,
                "legacy/{id:int}",
                null,
                redirectTargetRouteId: "profile"),
            Route("profile", "profile/{id:int}"),
        ]);
        var scope = new NavigationScope(graph);

        var result = await scope.NavigateByUriAsync(
            new Uri("atomui://boxer/legacy/42?tab=activity#summary"));

        Assert.Equal(NavigationResultStatus.Redirected, result.Status);
        Assert.Equal("42", result.Parameters["id"]);
        Assert.Equal("activity", result.Parameters["tab"]);
        Assert.Equal("summary", result.Parameters["fragment"]);
    }

    [Fact]
    public async Task StaticRedirectHonorsMatchPolicyAndFallsThrough()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            new RouteDescriptor(
                "legacy",
                RouteDefinitionKind.Redirect,
                "shared",
                null,
                redirectTargetRouteId: "target",
                matchPolicyTypes: [typeof(RejectingMatchPolicy)]),
            Route("fallback", "shared"),
            Route("target", "target"),
        ]);
        var scope = new NavigationScope(graph, type => type == typeof(RejectingMatchPolicy) ? new RejectingMatchPolicy() : null);

        var result = await scope.NavigateByPathAsync("shared");
        var byId = await scope.NavigateAsync(new RouteReference("legacy"));

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("fallback", result.Route.RouteId);
        Assert.Equal("legacy", graph.Matcher.Match("shared").Route.RouteId);
        Assert.Equal(NavigationResultStatus.Rejected, byId.Status);
    }

    [Fact]
    public async Task NavigationOutletSelectsRouteWithinRequestedOutlet()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            Route("primary", "settings"),
            Route("side", "settings", outletName: "side"),
        ]);
        var scope = new NavigationScope(graph);

        var primary = await scope.NavigateByPathAsync("settings");
        var side = await scope.NavigateByPathAsync(
            "settings",
            new NavigationOptions { OutletName = "side" });

        Assert.Equal("primary", primary.Route.RouteId);
        Assert.Equal("side", side.Route.RouteId);
        Assert.Equal("side", graph.Matcher.Match("settings", "side").Route.RouteId);
    }

    [Fact]
    public void EmptyContributionIsRejectedBeforeRegistryMutation()
    {
        Assert.Throws<ArgumentException>(() => new RouteContribution("empty", []));

        var graph = RouteGraphSnapshot.Create([]);
        var exception = Assert.Throws<RouteGraphException>(() => graph.WithContribution("empty", []));
        Assert.Equal(RouteGraphError.InvalidContribution, exception.Error);
    }

    [Fact]
    public void UnknownRouteKindAndTemplatelessGroupAreRejected()
    {
        var unknown = new RouteDescriptor("unknown", (RouteDefinitionKind)999, null, null);
        var group = new RouteDescriptor("group", RouteDefinitionKind.Group, null, null);

        Assert.Equal(
            RouteGraphError.InvalidRouteDefinition,
            Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create([unknown])).Error);
        Assert.Equal(
            RouteGraphError.InvalidRouteDefinition,
            Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create([group])).Error);
    }

    [Fact]
    public async Task MissingResolverServiceProducesResolverDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var scope = new NavigationScope(
            RouteGraphSnapshot.Create([Route("profile", "profile", resolverTypes: [typeof(CountingDataResolver)])]),
            diagnostics: diagnostics);

        var result = await scope.NavigateByPathAsync("profile");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        var record = Assert.Single(
            diagnostics.Records,
            item => item.Code == RoutingDiagnosticIds.PipelineComponentFailed);
        Assert.Equal("resolver", record.Context["stage"]);
        Assert.Equal(typeof(CountingDataResolver).FullName, record.Context["componentType"]);
    }

    [Fact]
    public async Task CancelGuardDoesNotEmitRejectedDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var scope = new NavigationScope(
            RouteGraphSnapshot.Create([Route("cancel", "cancel", enterGuardTypes: [typeof(CancellingGuard)])]),
            type => type == typeof(CancellingGuard) ? new CancellingGuard() : null,
            diagnostics);

        var result = await scope.NavigateByPathAsync("cancel");

        Assert.Equal(NavigationResultStatus.Cancelled, result.Status);
        Assert.DoesNotContain(
            diagnostics.Records,
            item => item.Code == RoutingDiagnosticIds.PipelineComponentRejected);
    }

    [Fact]
    public async Task CompletionDiagnosticUsesTransactionGraphVersion()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var resolver = new ControlledResolver(fail: true);
        var registry = new RouteRegistry(
            RouteGraphSnapshot.Create([Route("slow", "slow", resolverTypes: [typeof(ControlledResolver)])]),
            diagnostics);
        var scope = new NavigationScope(
            registry,
            type => type == typeof(ControlledResolver) ? resolver : null,
            diagnostics);
        var navigation = scope.NavigateByPathAsync("slow").AsTask();
        await resolver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var lease = registry.AddContribution(
            "plugin.extra",
            [Route("extra", "extra", contributionId: "plugin.extra")]);
        resolver.Release.TrySetResult();

        var result = await navigation;

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        var completed = Assert.Single(
            diagnostics.Records,
            item => item.Code == RoutingDiagnosticIds.NavigationFailed &&
                item.Context["operationId"] == result.NavigationId.ToString("D"));
        Assert.Equal("1", completed.Context["graphVersion"]);
    }

    [Fact]
    public async Task ForceReloadReentersSharedRouteHierarchy()
    {
        var guard = new CountingGuard();
        var graph = RouteGraphSnapshot.Create(
        [
            Route(
                "home",
                "home",
                enterGuardTypes: [typeof(CountingGuard)],
                leaveGuardTypes: [typeof(CountingGuard)]),
        ]);
        var scope = new NavigationScope(graph, type => type == typeof(CountingGuard) ? guard : null);

        await scope.NavigateByPathAsync("home");
        await scope.NavigateByPathAsync("home");
        await scope.NavigateByPathAsync("home", new NavigationOptions { ForceReload = true });

        Assert.Equal(2, guard.EnterCount);
        Assert.Equal(1, guard.LeaveCount);
    }

    [Fact]
    public async Task ResetNavigationClearsPriorJournalHistory()
    {
        var scope = new NavigationScope(RouteGraphSnapshot.Create(
        [
            Route("home", "home"),
            Route("profile", "profile"),
            Route("settings", "settings"),
        ]));
        await scope.NavigateByPathAsync("home");
        await scope.NavigateByPathAsync("profile");

        await scope.NavigateByPathAsync("settings", new NavigationOptions { Mode = NavigationMode.Reset });
        var back = await scope.BackAsync();

        Assert.Equal(NavigationResultStatus.Rejected, back.Status);
        Assert.Equal("CITY-NAVIGATION-JOURNAL-NOT-AVAILABLE", back.Error?.Code);
        Assert.Equal("settings", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task ResolverRedirectCompletesAtRedirectTarget()
    {
        var redirect = new RedirectingResolver();
        var graph = RouteGraphSnapshot.Create(
        [
            Route("source", "source", resolverTypes: [typeof(RedirectingResolver)]),
            Route("target", "target"),
        ]);
        var scope = new NavigationScope(graph, type => type == typeof(RedirectingResolver) ? redirect : null);

        var result = await scope.NavigateByPathAsync("source");

        Assert.Equal(NavigationResultStatus.Redirected, result.Status);
        Assert.Equal("target", result.Route.RouteId);
    }

    [Theory]
    [InlineData(RouteResolveResultStatus.Cancelled, NavigationResultStatus.Cancelled, "CITY-NAVIGATION-CANCELLED")]
    [InlineData(RouteResolveResultStatus.NotFound, NavigationResultStatus.Failed, "CITY-NAVIGATION-RESOLVER-NOT-FOUND")]
    public async Task ResolverNonSuccessStatusDoesNotCommit(
        RouteResolveResultStatus resolverStatus,
        NavigationResultStatus expectedStatus,
        string expectedCode)
    {
        var resolver = new StatusResolver(resolverStatus);
        var graph = RouteGraphSnapshot.Create(
        [
            Route("home", "home"),
            Route("target", "target", resolverTypes: [typeof(StatusResolver)]),
        ]);
        var scope = new NavigationScope(graph, type => type == typeof(StatusResolver) ? resolver : null);
        await scope.NavigateByPathAsync("home");

        var result = await scope.NavigateByPathAsync("target");

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedCode, result.Error?.Code);
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task ResolversRunRootToLeafAndInDeclarationOrder()
    {
        var calls = new List<string>();
        var graph = RouteGraphSnapshot.Create(
        [
            new RouteDescriptor(
                "shell",
                RouteDefinitionKind.Layout,
                null,
                new ViewModelTargetDescriptor(typeof(TestViewModel)),
                resolverTypes: [typeof(ParentFirstResolver), typeof(ParentSecondResolver)]),
            Route("child", "child", parentRouteId: "shell", resolverTypes: [typeof(ChildResolver)]),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(ParentFirstResolver)
                ? new ParentFirstResolver(calls)
                : type == typeof(ParentSecondResolver)
                    ? new ParentSecondResolver(calls)
                    : new ChildResolver(calls));

        var result = await scope.NavigateByPathAsync("child");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal(["parent-1", "parent-2", "child"], calls);
    }

    [Fact]
    public async Task NullResolverResultIsAttributedToResolverStage()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var graph = RouteGraphSnapshot.Create(
        [
            Route("target", "target", resolverTypes: [typeof(NullResolver)]),
        ]);
        var scope = new NavigationScope(graph, type => type == typeof(NullResolver) ? new NullResolver() : null, diagnostics);

        var result = await scope.NavigateByPathAsync("target");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        var diagnostic = Assert.Single(
            diagnostics.Records,
            item => item.Code == RoutingDiagnosticIds.PipelineComponentFailed);
        Assert.Equal("resolver", diagnostic.Context["stage"]);
    }

    private static RouteDescriptor Route(
        string id,
        string template,
        string? parentRouteId = null,
        string outletName = "primary",
        string? contributionId = null,
        IReadOnlyList<Type>? enterGuardTypes = null,
        IReadOnlyList<Type>? leaveGuardTypes = null,
        IReadOnlyList<Type>? resolverTypes = null,
        IReadOnlyList<Type>? middlewareTypes = null) =>
        new(
            id,
            RouteDefinitionKind.Route,
            template,
            new ViewModelTargetDescriptor(typeof(TestViewModel)),
            parentRouteId,
            outletName,
            contributionId: contributionId,
            enterGuardTypes: enterGuardTypes,
            leaveGuardTypes: leaveGuardTypes,
            resolverTypes: resolverTypes,
            middlewareTypes: middlewareTypes);

    private sealed class PostNextThrowingMiddleware : IRouteNavigationMiddleware
    {
        public async ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken)
        {
            _ = await next();
            throw new InvalidOperationException("Post-next failure.");
        }
    }

    private sealed class RepeatedNextMiddleware : IRouteNavigationMiddleware
    {
        public async ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken)
        {
            var first = await next();
            try
            {
                _ = await next();
            }
            catch (InvalidOperationException)
            {
            }

            return first;
        }
    }

    private sealed class CountingDataResolver : IRouteResolver
    {
        public int InvocationCount { get; private set; }

        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            return ValueTask.FromResult(RouteResolveResult.Success(
                new Dictionary<string, object?> { ["parent-generation"] = InvocationCount }));
        }
    }

    private sealed class ControlledResolver(bool fail = false) : IRouteResolver
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Release.Task;
            return fail
                ? RouteResolveResult.Failed("CONTROLLED-FAILURE", "Controlled resolver failed.")
                : RouteResolveResult.Success();
        }
    }

    private sealed class DelayedNavigationMiddleware : IRouteNavigationMiddleware
    {
        public NavigationScope? Scope { get; set; }
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<NavigationResult>? DelayedNavigation { get; private set; }

        public ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken)
        {
            DelayedNavigation = Task.Run(async () =>
            {
                await Release.Task;
                return await Scope!.NavigateByPathAsync("second");
            });
            return next();
        }
    }

    private sealed class CancelFirstLeaveGuard : IRouteLeaveGuard
    {
        private int _invocationCount;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<RouteGuardResult> CanLeaveAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _invocationCount) == 1)
            {
                Entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return RouteGuardResult.Allow();
        }
    }

    private sealed class RejectingMatchPolicy : IRouteMatchPolicy
    {
        public ValueTask<bool> CanMatchAsync(
            RouteMatchPolicyContext context,
            CancellationToken cancellationToken) => ValueTask.FromResult(false);
    }

    private sealed class CancellingGuard : IRouteEnterGuard
    {
        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken) => ValueTask.FromResult(RouteGuardResult.Cancel());
    }

    private sealed class CountingGuard : IRouteEnterGuard, IRouteLeaveGuard
    {
        public int EnterCount { get; private set; }
        public int LeaveCount { get; private set; }

        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            EnterCount++;
            return ValueTask.FromResult(RouteGuardResult.Allow());
        }

        public ValueTask<RouteGuardResult> CanLeaveAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            LeaveCount++;
            return ValueTask.FromResult(RouteGuardResult.Allow());
        }
    }

    private sealed class RedirectingResolver : IRouteResolver
    {
        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RouteResolveResult.Redirect(
                NavigationTarget.FromRouteReference("target", null, context.Target.Options)));
    }

    private sealed class StatusResolver(RouteResolveResultStatus status) : IRouteResolver
    {
        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken) => ValueTask.FromResult(status switch
            {
                RouteResolveResultStatus.Cancelled => RouteResolveResult.Cancelled(),
                RouteResolveResultStatus.NotFound => RouteResolveResult.NotFound(),
                _ => throw new InvalidOperationException("Unsupported test status."),
            });
    }

    private abstract class RecordingResolver(List<string> calls, string value) : IRouteResolver
    {
        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken)
        {
            calls.Add(value);
            return ValueTask.FromResult(RouteResolveResult.Success());
        }
    }

    private sealed class ParentFirstResolver(List<string> calls) : RecordingResolver(calls, "parent-1");
    private sealed class ParentSecondResolver(List<string> calls) : RecordingResolver(calls, "parent-2");
    private sealed class ChildResolver(List<string> calls) : RecordingResolver(calls, "child");

    private sealed class NullResolver : IRouteResolver
    {
        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken) => ValueTask.FromResult<RouteResolveResult>(null!);
    }

    private sealed class TestViewModel;
}
