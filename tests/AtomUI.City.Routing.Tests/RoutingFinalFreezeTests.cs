namespace AtomUI.City.Routing.Tests;

public sealed class RoutingFinalFreezeTests
{
    [Fact]
    public async Task PolicyCandidateRunsBeforeUnconditionalFallback()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            Route("a-fallback", "items/{value}"),
            Route("z-policy", "items/{id}", matchPolicyTypes: [typeof(AllowMatchPolicy)]),
        ]);
        var scope = new NavigationScope(graph, _ => new AllowMatchPolicy());

        var result = await scope.NavigateByPathAsync("items/42");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("z-policy", result.Route.RouteId);
    }

    [Fact]
    public void GraphRejectsMultipleUnconditionalCandidatesEvenWhenPolicyCandidateExists()
    {
        var exception = Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create(
        [
            Route("policy", "items/{id}", matchPolicyTypes: [typeof(AllowMatchPolicy)]),
            Route("fallback-a", "items/{value}"),
            Route("fallback-b", "items/{name}"),
        ]));

        Assert.Equal(RouteGraphError.DuplicateRouteTemplate, exception.Error);
    }

    [Fact]
    public void GraphRejectsEquivalentEffectiveTemplatesAcrossDifferentHierarchies()
    {
        var exception = Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create(
        [
            Group("area", "account"),
            Route("nested", "settings", parentRouteId: "area"),
            Route("flat", "account/settings"),
        ]));

        Assert.Equal(RouteGraphError.DuplicateRouteTemplate, exception.Error);
    }

    [Fact]
    public void GraphTreatsDefaultedAndOptionalParametersAsEquivalent()
    {
        var exception = Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create(
        [
            Route("optional", "items/{id?}"),
            Route("defaulted", "items/{value=42}"),
        ]));

        Assert.Equal(RouteGraphError.DuplicateRouteTemplate, exception.Error);
    }

    [Fact]
    public void GraphRejectsRedirectToNonNavigableRoute()
    {
        var exception = Assert.Throws<RouteGraphException>(() => RouteGraphSnapshot.Create(
        [
            Group("group", "account"),
            Redirect("legacy", "legacy", "group"),
        ]));

        Assert.Equal(RouteGraphError.InvalidRouteDefinition, exception.Error);
    }

    [Fact]
    public void ContributionRejectsNullDescriptorWithStableGraphError()
    {
        var graph = RouteGraphSnapshot.Create([]);

        var exception = Assert.Throws<RouteGraphException>(() => graph.WithContribution(
            "plugin.routes",
            new RouteDescriptor[] { null! }));

        Assert.Equal(RouteGraphError.InvalidContribution, exception.Error);
    }

    [Fact]
    public void RegexConstraintSupportsEscapedParenthesesAndCommaQuantifier()
    {
        var template = RouteTemplate.Parse(@"value/{code:regex(^\(a{1,3}\)$)}");

        Assert.True(template.TryMatch("value/(aa)", out var parameters));
        Assert.Equal("(aa)", parameters["code"]);
        Assert.False(template.TryMatch("value/aa", out _));
    }

    [Fact]
    public async Task RedirectLoopIdentityDoesNotCollideOnParameterDelimiters()
    {
        var guard = new DelimiterRedirectGuard();
        var graph = RouteGraphSnapshot.Create(
        [
            Route("start", "start", enterGuardTypes: [typeof(DelimiterRedirectGuard)]),
            Route("middle", "middle", enterGuardTypes: [typeof(DelimiterRedirectGuard)]),
            Route("final", "final"),
        ]);
        var scope = new NavigationScope(graph, _ => guard);

        var result = await scope.NavigateByPathAsync("start");

        Assert.Equal(NavigationResultStatus.Redirected, result.Status);
        Assert.Equal("final", result.Route.RouteId);
    }

    [Fact]
    public async Task DynamicRedirectInheritsOptionsAndMergesParameters()
    {
        var options = new NavigationOptions
        {
            OutletName = "side",
            Mode = NavigationMode.Replace,
            ForceReload = true,
        };
        var guard = new ContextRedirectGuard();
        var graph = RouteGraphSnapshot.Create(
        [
            Route("source", "source", outletName: "side", enterGuardTypes: [typeof(ContextRedirectGuard)]),
            Route("final", "final", outletName: "side"),
        ]);
        var scope = new NavigationScope(graph, _ => guard);

        var result = await scope.NavigateAsync(
            new RouteReference<SourceParameters>("source", static value =>
                new Dictionary<string, string>
                {
                    ["tenant"] = value.Tenant,
                    ["shared"] = value.Shared,
                }),
            new SourceParameters("tenant-a", "source"),
            options);

        Assert.Equal(NavigationResultStatus.Redirected, result.Status);
        Assert.Equal("final", result.Route.RouteId);
        Assert.Same(options, result.RedirectTarget!.Options);
        Assert.Equal("tenant-a", result.Parameters["tenant"]);
        Assert.Equal("redirect", result.Parameters["shared"]);
    }

    [Fact]
    public async Task NestedMiddlewareCannotInvokeNextAfterItsOwnReturn()
    {
        var outer = new HoldingOuterMiddleware();
        var inner = new CapturingRejectMiddleware();
        var graph = RouteGraphSnapshot.Create(
        [
            Route("target", "target", middlewareTypes: [typeof(HoldingOuterMiddleware), typeof(CapturingRejectMiddleware)]),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(HoldingOuterMiddleware) ? outer : inner);

        var navigation = scope.NavigateByPathAsync("target").AsTask();
        await outer.DownstreamReturned.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await inner.Next!().AsTask());
        outer.Release.TrySetResult();
        var result = await navigation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        Assert.Equal("CITY-NAVIGATION-MIDDLEWARE-INVALID-RESULT", result.Error?.Code);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task MiddlewareCannotInvokeShortCircuitingNextTwice()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            Route("target", "target", middlewareTypes: [typeof(RepeatNextMiddleware), typeof(RejectMiddleware)]),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(RepeatNextMiddleware) ? new RepeatNextMiddleware() : new RejectMiddleware());

        var result = await scope.NavigateByPathAsync("target");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        Assert.Equal("CITY-NAVIGATION-MIDDLEWARE-INVALID-RESULT", result.Error?.Code);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task MiddlewareResultMustBelongToCurrentNavigationOperation()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            Route("target", "target", middlewareTypes: [typeof(ForeignResultMiddleware)]),
        ]);
        var scope = new NavigationScope(graph, _ => new ForeignResultMiddleware());

        var result = await scope.NavigateByPathAsync("target");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        Assert.Equal("CITY-NAVIGATION-MIDDLEWARE-INVALID-RESULT", result.Error?.Code);
    }

    [Fact]
    public async Task BackRedirectStoresCommittedRouteInJournal()
    {
        var provider = new MutableRouteGraphProvider(RouteGraphSnapshot.Create(
        [
            Route("legacy", "legacy"),
            Route("current", "current"),
            Route("replacement", "replacement"),
        ], version: 1));
        var scope = new NavigationScope(provider);
        await scope.NavigateByPathAsync("legacy");
        await scope.NavigateByPathAsync("current");

        provider.CurrentSnapshot = RouteGraphSnapshot.Create(
        [
            Redirect("legacy", "legacy", "replacement"),
            Route("current", "current"),
            Route("replacement", "replacement"),
        ], version: 2);
        Assert.Equal("replacement", (await scope.BackAsync()).Route.RouteId);
        Assert.Equal("current", (await scope.ForwardAsync()).Route.RouteId);

        provider.CurrentSnapshot = RouteGraphSnapshot.Create(
        [
            Route("current", "current"),
            Route("replacement", "replacement"),
        ], version: 3);
        var result = await scope.BackAsync();

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("replacement", result.Route.RouteId);
    }

    [Fact]
    public async Task BackSkipsMissingRouteAfterContributionIdIsReused()
    {
        var provider = new MutableRouteGraphProvider(RouteGraphSnapshot.Create(
        [
            Route("plugin.old", "old", contributionId: "plugin.routes"),
            Route("home", "home"),
        ], version: 1));
        var scope = new NavigationScope(provider);
        await scope.NavigateByPathAsync("old");
        await scope.NavigateByPathAsync("home");

        provider.CurrentSnapshot = RouteGraphSnapshot.Create(
        [
            Route("plugin.new", "new", contributionId: "plugin.routes"),
            Route("home", "home"),
        ], version: 2);
        var result = await scope.BackAsync();

        Assert.Equal(NavigationResultStatus.Rejected, result.Status);
        Assert.Equal("CITY-NAVIGATION-JOURNAL-NOT-AVAILABLE", result.Error?.Code);
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task DisposeAndAdmissionRaceDoesNotLeakDisposedPrimitives()
    {
        for (var iteration = 0; iteration < 100; iteration++)
        {
            var resolver = new YieldingResolver();
            var graph = RouteGraphSnapshot.Create(
            [
                Route("target", "target", resolverTypes: [typeof(YieldingResolver)]),
            ]);
            var scope = new NavigationScope(graph, _ => resolver);

            var navigation = Task.Run(async () => await scope.NavigateByPathAsync("target"));
            var disposal = Task.Run(async () => await scope.DisposeAsync());
            var result = await navigation.WaitAsync(TimeSpan.FromSeconds(5));
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.NotEqual(NavigationResultStatus.Failed, result.Status);
            Assert.NotEqual(typeof(ObjectDisposedException), result.Error?.Exception?.GetType());
        }
    }

    private static RouteDescriptor Route(
        string id,
        string template,
        string? parentRouteId = null,
        string outletName = "primary",
        string? contributionId = null,
        IReadOnlyList<Type>? enterGuardTypes = null,
        IReadOnlyList<Type>? matchPolicyTypes = null,
        IReadOnlyList<Type>? resolverTypes = null,
        IReadOnlyList<Type>? middlewareTypes = null) =>
        new(
            id,
            RouteDefinitionKind.Route,
            template,
            new ViewModelTargetDescriptor(typeof(TestViewModel)),
            parentRouteId,
            outletName,
            enterGuardTypes: enterGuardTypes,
            matchPolicyTypes: matchPolicyTypes,
            contributionId: contributionId,
            resolverTypes: resolverTypes,
            middlewareTypes: middlewareTypes);

    private static RouteDescriptor Group(string id, string template) =>
        new(id, RouteDefinitionKind.Group, template, viewModelTarget: null);

    private static RouteDescriptor Redirect(string id, string template, string targetRouteId) =>
        new(
            id,
            RouteDefinitionKind.Redirect,
            template,
            viewModelTarget: null,
            redirectTargetRouteId: targetRouteId);

    private sealed class AllowMatchPolicy : IRouteMatchPolicy
    {
        public ValueTask<bool> CanMatchAsync(
            RouteMatchPolicyContext context,
            CancellationToken cancellationToken) => ValueTask.FromResult(true);
    }

    private sealed class DelimiterRedirectGuard : IRouteEnterGuard
    {
        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken)
        {
            if (context.Route.RouteId == "start")
            {
                return RedirectTo("middle", new Dictionary<string, string> { ["a"] = "b&c=d" });
            }

            if (!context.Parameters.ContainsKey("c"))
            {
                return RedirectTo("middle", new Dictionary<string, string> { ["a"] = "b", ["c"] = "d" });
            }

            return RedirectTo("final", null);
        }
    }

    private sealed class ContextRedirectGuard : IRouteEnterGuard
    {
        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken) =>
            RedirectTo("final", new Dictionary<string, string> { ["shared"] = "redirect" });
    }

    private static ValueTask<RouteGuardResult> RedirectTo(
        string routeId,
        IReadOnlyDictionary<string, string>? parameters) =>
        ValueTask.FromResult(RouteGuardResult.Redirect(
            NavigationTarget.FromRouteReference(routeId, parameters, NavigationOptions.Default)));

    private sealed class HoldingOuterMiddleware : IRouteNavigationMiddleware
    {
        public TaskCompletionSource DownstreamReturned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken)
        {
            var result = await next();
            DownstreamReturned.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class CapturingRejectMiddleware : IRouteNavigationMiddleware
    {
        public RouteNavigationDelegate? Next { get; private set; }

        public ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken)
        {
            Next = next;
            return ValueTask.FromResult(NavigationResult.Rejected(
                context.NavigationId,
                context.Target,
                "MIDDLEWARE-STOP"));
        }
    }

    private sealed class RepeatNextMiddleware : IRouteNavigationMiddleware
    {
        public async ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken)
        {
            var result = await next();
            try
            {
                await next();
            }
            catch (InvalidOperationException)
            {
            }

            return result;
        }
    }

    private sealed class RejectMiddleware : IRouteNavigationMiddleware
    {
        public ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(NavigationResult.Rejected(
                context.NavigationId,
                context.Target,
                "MIDDLEWARE-STOP"));
    }

    private sealed class ForeignResultMiddleware : IRouteNavigationMiddleware
    {
        public ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(NavigationResult.Rejected(
                Guid.NewGuid(),
                context.Target,
                "FOREIGN"));
    }

    private sealed class YieldingResolver : IRouteResolver
    {
        public async ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return RouteResolveResult.Success();
        }
    }

    private sealed class MutableRouteGraphProvider(RouteGraphSnapshot snapshot) : IRouteGraphProvider
    {
        public RouteGraphSnapshot CurrentSnapshot { get; set; } = snapshot;
    }

    private sealed record SourceParameters(string Tenant, string Shared);
    private sealed class TestViewModel;
}
