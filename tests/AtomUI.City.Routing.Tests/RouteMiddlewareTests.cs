using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Routing.Tests;

public sealed class RouteMiddlewareTests
{
    [Fact]
    public async Task MiddlewareRunsParentToChildOnEntryAndChildToParentOnExit()
    {
        var calls = new List<string>();
        var parent = new RecordingMiddleware("parent", calls);
        var child = new RecordingMiddleware("child", calls);
        var graph = RouteGraphSnapshot.Create(
        [
            new RouteDescriptor(
                "shell",
                RouteDefinitionKind.Layout,
                null,
                new ViewModelTargetDescriptor(typeof(TestViewModel)),
                middlewareTypes: [typeof(ParentMiddleware)]),
            new RouteDescriptor(
                "profile",
                RouteDefinitionKind.Route,
                "profile",
                new ViewModelTargetDescriptor(typeof(TestViewModel)),
                parentRouteId: "shell",
                middlewareTypes: [typeof(ChildMiddleware)]),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(ParentMiddleware)
                ? new ParentMiddleware(parent)
                : new ChildMiddleware(child));

        var result = await scope.NavigateByPathAsync("profile");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal(["parent:enter", "child:enter", "child:exit", "parent:exit"], calls);
    }

    [Fact]
    public async Task MiddlewareCanShortCircuitWithoutCommittingSnapshot()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            new RouteDescriptor(
                "blocked",
                RouteDefinitionKind.Route,
                "blocked",
                new ViewModelTargetDescriptor(typeof(TestViewModel)),
                middlewareTypes: [typeof(RejectingMiddleware)]),
        ]);
        var scope = new NavigationScope(graph, type => new RejectingMiddleware());

        var result = await scope.NavigateByPathAsync("blocked");

        Assert.Equal(NavigationResultStatus.Rejected, result.Status);
        Assert.Equal("MIDDLEWARE-BLOCKED", result.Error?.Code);
        Assert.Null(scope.CurrentSnapshot.ActiveRoute);
    }

    [Fact]
    public async Task MiddlewareFailureWritesComponentDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var graph = RouteGraphSnapshot.Create(
        [
            new RouteDescriptor(
                "failed",
                RouteDefinitionKind.Route,
                "failed",
                new ViewModelTargetDescriptor(typeof(TestViewModel)),
                middlewareTypes: [typeof(ThrowingMiddleware)]),
        ]);
        var scope = new NavigationScope(graph, _ => new ThrowingMiddleware(), diagnostics);

        var result = await scope.NavigateByPathAsync("failed");

        Assert.Equal(NavigationResultStatus.Failed, result.Status);
        var diagnostic = Assert.Single(
            diagnostics.Records,
            record => record.Code == RoutingDiagnosticIds.PipelineComponentFailed);
        Assert.Equal("middleware", diagnostic.Context["stage"]);
        Assert.Equal(typeof(ThrowingMiddleware).FullName, diagnostic.Context["componentType"]);
    }

    private sealed class RecordingMiddleware(string name, List<string> calls)
    {
        public async ValueTask<NavigationResult> InvokeAsync(RouteNavigationDelegate next)
        {
            calls.Add(name + ":enter");
            var result = await next();
            calls.Add(name + ":exit");
            return result;
        }
    }

    private sealed class ParentMiddleware(RecordingMiddleware inner) : IRouteNavigationMiddleware
    {
        public ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken) => inner.InvokeAsync(next);
    }

    private sealed class ChildMiddleware(RecordingMiddleware inner) : IRouteNavigationMiddleware
    {
        public ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken) => inner.InvokeAsync(next);
    }

    private sealed class RejectingMiddleware : IRouteNavigationMiddleware
    {
        public ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(NavigationResult.Rejected(
                context.NavigationId,
                context.Target,
                "MIDDLEWARE-BLOCKED"));
    }

    private sealed class ThrowingMiddleware : IRouteNavigationMiddleware
    {
        public ValueTask<NavigationResult> InvokeAsync(
            RouteNavigationMiddlewareContext context,
            RouteNavigationDelegate next,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Middleware failed.");
    }

    private sealed class TestViewModel;
}
