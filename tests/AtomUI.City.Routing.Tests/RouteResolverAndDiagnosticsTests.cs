using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Routing.Tests;

public sealed class RouteResolverAndDiagnosticsTests
{
    [Fact]
    public async Task ResolverDataIsCommittedAtomicallyWithNavigationSnapshot()
    {
        var resolver = new SuccessfulResolver();
        var diagnostics = new InMemoryHostDiagnostics();
        var scope = CreateScope([typeof(SuccessfulResolver)], resolver, diagnostics);

        var result = await scope.NavigateByPathAsync("profile/42");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("Ada", scope.CurrentSnapshot.ResolvedData["displayName"]);
        Assert.Equal("42", resolver.Parameters!["id"]);
        Assert.Contains(diagnostics.Records, record => record.Code == RoutingDiagnosticIds.NavigationStarted);
        var completed = Assert.Single(
            diagnostics.Records,
            record => record.Code == RoutingDiagnosticIds.NavigationCompleted);
        Assert.Equal(result.NavigationId.ToString("D"), completed.Context["operationId"]);
        Assert.Equal("1", completed.Context["graphVersion"]);
        Assert.NotNull(completed.Context["elapsedMilliseconds"]);
    }

    [Fact]
    public async Task ResolverFailureKeepsPreviousSnapshotAndWritesResolverDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var resolver = new FailedResolver();
        var graph = RouteGraphSnapshot.Create(
        [
            Route("home", "home"),
            Route("profile", "profile", [typeof(FailedResolver)]),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(FailedResolver) ? resolver : null,
            diagnostics);
        await scope.NavigateByPathAsync("home");

        var result = await scope.NavigateByPathAsync("profile");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        Assert.Equal("PROFILE-LOAD-FAILED", result.Error?.Code);
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
        var diagnostic = Assert.Single(
            diagnostics.Records,
            record => record.Code == RoutingDiagnosticIds.ResolverFailed);
        Assert.Equal(typeof(FailedResolver).FullName, diagnostic.Context["resolverType"]);
        Assert.Equal("profile", diagnostic.Context["routeId"]);
    }

    [Fact]
    public async Task DuplicateResolverDataKeysFailBeforeCommit()
    {
        var first = new ConstantResolver("shared", 1);
        var second = new ConstantResolver("shared", 2);
        var route = Route("profile", "profile", [typeof(FirstResolver), typeof(SecondResolver)]);
        var scope = new NavigationScope(
            RouteGraphSnapshot.Create([route]),
            type => type == typeof(FirstResolver) ? new FirstResolver(first) : new SecondResolver(second));

        var result = await scope.NavigateByPathAsync("profile");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        Assert.Equal("CITY-NAVIGATION-RESOLVER-DUPLICATE-KEY", result.Error?.Code);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task ResolverExceptionInsideMiddlewareIsNotMisattributedToMiddleware()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var graph = RouteGraphSnapshot.Create(
        [
            new RouteDescriptor(
                "profile",
                RouteDefinitionKind.Route,
                "profile",
                new ViewModelTargetDescriptor(typeof(TestViewModel)),
                resolverTypes: [typeof(ThrowingResolver)],
                middlewareTypes: [typeof(PassThroughMiddleware)]),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(ThrowingResolver)
                ? new ThrowingResolver()
                : new PassThroughMiddleware(),
            diagnostics);

        var result = await scope.NavigateByPathAsync("profile");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        var diagnostic = Assert.Single(
            diagnostics.Records,
            record => record.Code == RoutingDiagnosticIds.PipelineComponentFailed);
        Assert.Equal("resolver", diagnostic.Context["stage"]);
        Assert.Equal(typeof(ThrowingResolver).FullName, diagnostic.Context["componentType"]);
    }

    [Fact]
    public async Task ReusedExceptionInstanceIsDiagnosedOncePerNavigation()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var resolver = new CachedExceptionResolver();
        var graph = RouteGraphSnapshot.Create(
        [
            new RouteDescriptor(
                "profile",
                RouteDefinitionKind.Route,
                "profile",
                new ViewModelTargetDescriptor(typeof(TestViewModel)),
                resolverTypes: [typeof(CachedExceptionResolver)],
                middlewareTypes: [typeof(PassThroughMiddleware)]),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(CachedExceptionResolver)
                ? resolver
                : new PassThroughMiddleware(),
            diagnostics);

        await scope.NavigateByPathAsync("profile");
        await scope.NavigateByPathAsync("profile");

        Assert.Equal(
            2,
            diagnostics.Records.Count(
                record => record.Code == RoutingDiagnosticIds.PipelineComponentFailed));
    }

    [Fact]
    public async Task CompletedDiagnosticsSinkCannotBreakSuccessfulNavigation()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        diagnostics.Complete();
        var scope = new NavigationScope(
            RouteGraphSnapshot.Create([Route("home", "home")]),
            diagnostics: diagnostics);

        var result = await scope.NavigateByPathAsync("home");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
    }

    [Fact]
    public void RegistryPublishesGraphDiagnosticsWithoutDependingOnSinkHealth()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new RouteRegistry(RouteGraphSnapshot.Create([]), diagnostics);
        using var lease = registry.AddContribution(
            "plugin.profile",
            [Route("profile", "profile", contributionId: "plugin.profile")]);

        var added = Assert.Single(diagnostics.Records);
        Assert.Equal(RoutingDiagnosticIds.RouteGraphChanged, added.Code);
        Assert.Equal("added", added.Context["operation"]);

        diagnostics.Complete();
        lease.Dispose();
        Assert.Empty(registry.CurrentSnapshot.Routes);
    }

    private static NavigationScope CreateScope(
        IReadOnlyList<Type> resolverTypes,
        IRouteResolver resolver,
        IHostDiagnostics diagnostics)
    {
        return new NavigationScope(
            RouteGraphSnapshot.Create([Route("profile", "profile/{id:int}", resolverTypes)]),
            type => type == resolverTypes[0] ? resolver : null,
            diagnostics);
    }

    private static RouteDescriptor Route(
        string id,
        string template,
        IReadOnlyList<Type>? resolverTypes = null,
        string? contributionId = null) =>
        new(
            id,
            RouteDefinitionKind.Route,
            template,
            new ViewModelTargetDescriptor(typeof(TestViewModel)),
            contributionId: contributionId,
            resolverTypes: resolverTypes);

    private sealed class SuccessfulResolver : IRouteResolver
    {
        public IReadOnlyDictionary<string, string>? Parameters { get; private set; }

        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken)
        {
            Parameters = context.Parameters;
            return ValueTask.FromResult(RouteResolveResult.Success(
                new Dictionary<string, object?> { ["displayName"] = "Ada" }));
        }
    }

    private sealed class FailedResolver : IRouteResolver
    {
        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(RouteResolveResult.Failed(
                "PROFILE-LOAD-FAILED",
                "Profile data could not be loaded."));
    }

    private sealed class ThrowingResolver : IRouteResolver
    {
        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Resolver failed.");
    }

    private sealed class CachedExceptionResolver : IRouteResolver
    {
        private readonly InvalidOperationException _exception = new("Resolver failed.");

        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken) => throw _exception;
    }

    private sealed class PassThroughMiddleware : IRouteNavigationMiddleware
    {
        public ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken) => next();
    }

    private sealed class ConstantResolver(string key, object? value)
    {
        public ValueTask<RouteResolveResult> ResolveAsync() =>
            ValueTask.FromResult(RouteResolveResult.Success(
                new Dictionary<string, object?> { [key] = value }));
    }

    private sealed class FirstResolver(ConstantResolver inner) : IRouteResolver
    {
        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken) => inner.ResolveAsync();
    }

    private sealed class SecondResolver(ConstantResolver inner) : IRouteResolver
    {
        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken) => inner.ResolveAsync();
    }

    private sealed class TestViewModel;
}
