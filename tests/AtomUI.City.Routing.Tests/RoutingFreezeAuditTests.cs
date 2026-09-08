namespace AtomUI.City.Routing.Tests;

public sealed class RoutingFreezeAuditTests
{
    [Fact]
    public async Task StartedMiddlewareNextRemainsInsideNavigationTransaction()
    {
        var resolver = new BlockingResolver();
        var middleware = new DetachedNextMiddleware();
        var graph = RouteGraphSnapshot.Create(
        [
            Route(
                "target",
                "target",
                resolverTypes: [typeof(BlockingResolver)],
                middlewareTypes: [typeof(DetachedNextMiddleware)]),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(BlockingResolver) ? resolver : middleware);

        var navigation = scope.NavigateByPathAsync("target").AsTask();
        await resolver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(navigation.IsCompleted);

        resolver.Release.TrySetResult();
        var downstream = await middleware.Downstream!.WaitAsync(TimeSpan.FromSeconds(5));
        var result = await navigation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(NavigationResultStatus.Success, downstream.Status);
        Assert.Equal(NavigationResultStatus.Rejected, result.Status);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task CancellationOfStartedMiddlewareNextProducesCancelledResult()
    {
        var resolver = new BlockingResolver();
        var middleware = new DetachedNextMiddleware();
        var graph = RouteGraphSnapshot.Create(
        [
            Route(
                "target",
                "target",
                resolverTypes: [typeof(BlockingResolver)],
                middlewareTypes: [typeof(DetachedNextMiddleware)]),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(BlockingResolver) ? resolver : middleware);
        using var cancellation = new CancellationTokenSource();

        var navigation = scope.NavigateByPathAsync("target", cancellationToken: cancellation.Token).AsTask();
        await resolver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var result = await navigation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(NavigationResultStatus.Cancelled, result.Status);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task MiddlewareNextCannotBeInvokedAfterMiddlewareReturns()
    {
        var resolver = new CountingObjectResolver();
        var middleware = new CapturingNextMiddleware();
        var graph = RouteGraphSnapshot.Create(
        [
            Route(
                "target",
                "target",
                resolverTypes: [typeof(CountingObjectResolver)],
                middlewareTypes: [typeof(CapturingNextMiddleware)]),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(CountingObjectResolver) ? resolver : middleware);

        var result = await scope.NavigateByPathAsync("target");

        Assert.Equal(NavigationResultStatus.Rejected, result.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await middleware.Next!().AsTask());
        Assert.Equal(0, resolver.InvocationCount);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task UnrelatedOperationCanceledExceptionIsReportedAsFailure()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            Route("target", "target", enterGuardTypes: [typeof(UnrelatedCancellationGuard)]),
        ]);
        var scope = new NavigationScope(graph, _ => new UnrelatedCancellationGuard());

        var result = await scope.NavigateByPathAsync("target");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        Assert.IsType<OperationCanceledException>(result.Error?.Exception);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task JournalRerunsResolverWhenResolvedDataContainsNonScalarValue()
    {
        var resolver = new CountingObjectResolver();
        var graph = RouteGraphSnapshot.Create(
        [
            Route("profile", "profile", resolverTypes: [typeof(CountingObjectResolver)]),
            Route("settings", "settings"),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(CountingObjectResolver) ? resolver : null);

        await scope.NavigateByPathAsync("profile");
        await scope.NavigateByPathAsync("settings");
        var result = await scope.BackAsync();

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal(2, resolver.InvocationCount);
        var payload = Assert.IsType<ResolverPayload>(scope.CurrentSnapshot.ResolvedData["payload"]);
        Assert.Equal(2, payload.Generation);
    }

    [Fact]
    public void RouteContributionStampsUnownedGeneratedDescriptors()
    {
        var generatedRoute = Route("plugin.profile", "profile");
        var contribution = new RouteContribution("plugin.profile", [generatedRoute]);
        var ownedRoute = Assert.Single(contribution.Routes);

        Assert.Null(generatedRoute.ContributionId);
        Assert.Equal("plugin.profile", ownedRoute.ContributionId);

        var registry = new RouteRegistry();
        var lease = registry.AddContribution(contribution);
        Assert.Equal("plugin.profile", registry.CurrentSnapshot.GetRequiredRoute("plugin.profile").ContributionId);

        lease.Dispose();
        Assert.Empty(registry.CurrentSnapshot.Routes);
    }

    [Fact]
    public void RouteContributionRejectsDescriptorOwnedByAnotherContribution()
    {
        var exception = Assert.Throws<RouteGraphException>(
            () => new RouteContribution(
                "plugin.expected",
                [Route("plugin.route", "route", contributionId: "plugin.other")]));

        Assert.Equal(RouteGraphError.InvalidContribution, exception.Error);
    }

    [Fact]
    public void TemplateRejectsInvalidConstrainedDefaultAndAppliesCatchAllConstraints()
    {
        var exception = Assert.Throws<RouteGraphException>(
            () => RouteTemplate.Parse("items/{id:int=not-an-int}"));
        var catchAll = RouteTemplate.Parse("files/{*path:minlength(3)=all}");

        Assert.Equal(RouteGraphError.InvalidRouteTemplate, exception.Error);
        Assert.True(catchAll.TryMatch("files", out var defaultValues));
        Assert.Equal("all", defaultValues["path"]);
        Assert.False(catchAll.TryMatch("files/a", out _));
        Assert.True(catchAll.TryMatch("files/abc", out _));
    }

    [Fact]
    public void GraphRejectsNullDescriptorWithStableGraphError()
    {
        var exception = Assert.Throws<RouteGraphException>(
            () => RouteGraphSnapshot.Create([null!]));

        Assert.Equal(RouteGraphError.InvalidRouteDefinition, exception.Error);
    }

    [Fact]
    public void GraphUsesOrdinalStructuredParentAndOutletIdentity()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            Group("Parent", "upper"),
            Group("parent", "lower"),
            Route("upper-item", "item", parentRouteId: "Parent", outletName: "Side"),
            Route("lower-item", "item", parentRouteId: "parent", outletName: "side"),
            Group("a|b", "pipe-upper"),
            Group("a", "pipe-lower"),
            Route("pipe-upper-item", "item", parentRouteId: "a|b", outletName: "c"),
            Route("pipe-lower-item", "item", parentRouteId: "a", outletName: "b|c"),
        ]);

        Assert.Equal("upper-item", graph.Matcher.Match("upper/item", "Side").Route.RouteId);
        Assert.Equal("lower-item", graph.Matcher.Match("lower/item", "side").Route.RouteId);
        Assert.Equal("pipe-upper-item", graph.Matcher.Match("pipe-upper/item", "c").Route.RouteId);
        Assert.Equal("pipe-lower-item", graph.Matcher.Match("pipe-lower/item", "b|c").Route.RouteId);
    }

    [Fact]
    public void GraphRejectsRouteThatReferencesMissingExtensionPoint()
    {
        var exception = Assert.Throws<RouteGraphException>(
            () => RouteGraphSnapshot.Create(
            [
                Route("orphan", "orphan", extensionPoint: "missing.point"),
            ]));

        Assert.Equal(RouteGraphError.MissingExtensionPoint, exception.Error);
    }

    private static RouteDescriptor Route(
        string id,
        string template,
        string? parentRouteId = null,
        string outletName = "primary",
        string? extensionPoint = null,
        string? contributionId = null,
        IReadOnlyList<Type>? enterGuardTypes = null,
        IReadOnlyList<Type>? resolverTypes = null,
        IReadOnlyList<Type>? middlewareTypes = null) =>
        new(
            id,
            RouteDefinitionKind.Route,
            template,
            new ViewModelTargetDescriptor(typeof(TestViewModel)),
            parentRouteId,
            outletName,
            extensionPoint,
            contributionId: contributionId,
            enterGuardTypes: enterGuardTypes,
            resolverTypes: resolverTypes,
            middlewareTypes: middlewareTypes);

    private static RouteDescriptor Group(string id, string template) =>
        new(id, RouteDefinitionKind.Group, template, null);

    private sealed class DetachedNextMiddleware : IRouteNavigationMiddleware
    {
        public Task<NavigationResult>? Downstream { get; private set; }

        public ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken)
        {
            Downstream = next().AsTask();
            return ValueTask.FromResult(NavigationResult.Rejected(
                context.NavigationId,
                context.Target,
                "MIDDLEWARE-SHORT-CIRCUIT"));
        }
    }

    private sealed class CapturingNextMiddleware : IRouteNavigationMiddleware
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
                "MIDDLEWARE-SHORT-CIRCUIT"));
        }
    }

    private sealed class BlockingResolver : IRouteResolver
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return RouteResolveResult.Success();
        }
    }

    private sealed class CountingObjectResolver : IRouteResolver
    {
        public int InvocationCount { get; private set; }

        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            return ValueTask.FromResult(RouteResolveResult.Success(
                new Dictionary<string, object?>
                {
                    ["payload"] = new ResolverPayload(InvocationCount),
                }));
        }
    }

    private sealed class UnrelatedCancellationGuard : IRouteEnterGuard
    {
        public ValueTask<RouteGuardResult> CanEnterAsync(
            RouteGuardContext context,
            CancellationToken cancellationToken) =>
            throw new OperationCanceledException("Unrelated cancellation.");
    }

    private sealed record ResolverPayload(int Generation);

    private sealed class TestViewModel;
}
