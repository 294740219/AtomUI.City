using AtomUI.City.Routing;
using AtomUI.City.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Routing.Tests;

public sealed class RouteRegistryTests
{
    [Fact]
    public void ContributionLeasePublishesAndIdempotentlyRevokesSnapshot()
    {
        var initial = RouteGraphSnapshot.Create([Route("home", "home")], version: 7);
        var registry = new RouteRegistry(initial);
        var lease = registry.AddContribution(
            "plugin.settings",
            [Route("plugin-settings", "plugin/settings", "plugin.settings")]);
        var contributed = registry.CurrentSnapshot;

        Assert.Equal(8, contributed.Version);
        Assert.True(contributed.TryGetRoute("plugin-settings", out _));
        Assert.False(initial.TryGetRoute("plugin-settings", out _));

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(9, registry.CurrentSnapshot.Version);
        Assert.False(registry.CurrentSnapshot.TryGetRoute("plugin-settings", out _));
    }

    [Fact]
    public void FailedContributionDoesNotReplacePublishedSnapshot()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var registry = new RouteRegistry(RouteGraphSnapshot.Create([Route("home", "home")]), diagnostics);
        var before = registry.CurrentSnapshot;

        Assert.Throws<RouteGraphException>(
            () => registry.AddContribution(
                "plugin.conflict",
                [Route("plugin-home", "home", "plugin.conflict")]));

        Assert.Same(before, registry.CurrentSnapshot);
        var rejected = Assert.Single(diagnostics.Records);
        Assert.Equal(RoutingDiagnosticIds.RouteGraphRejected, rejected.Code);
        Assert.Equal(RouteGraphError.DuplicateRouteTemplate.ToString(), rejected.Context["graphError"]);
    }

    [Fact]
    public async Task NavigationScopeUsesLatestSnapshotForEachNavigation()
    {
        var registry = new RouteRegistry(RouteGraphSnapshot.Create([Route("home", "home")]));
        var scope = new NavigationScope(registry);

        Assert.Equal(NavigationResultStatus.NotFound, (await scope.NavigateByPathAsync("plugin/settings")).Status);

        using var lease = registry.AddContribution(
            "plugin.settings",
            [Route("plugin-settings", "plugin/settings", "plugin.settings")]);
        var result = await scope.NavigateByPathAsync("plugin/settings");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("plugin-settings", result.Route.RouteId);
        Assert.Equal(registry.CurrentSnapshot.Version, scope.CurrentSnapshot.RouteGraphVersion);
    }

    [Fact]
    public void AddRoutingRegistersScopedRouterAndSingletonRegistry()
    {
        var services = new ServiceCollection();
        services.AddRouting([Route("home", "home")]);
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<IRouter>(),
            firstScope.ServiceProvider.GetRequiredService<NavigationScope>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<IRouter>(),
            secondScope.ServiceProvider.GetRequiredService<IRouter>());
        Assert.Same(
            provider.GetRequiredService<IRouteRegistry>(),
            provider.GetRequiredService<IRouteGraphProvider>());
    }

    [Fact]
    public async Task ContributionServicesResolveFromPluginBoundaryAndAreRevokedWithLease()
    {
        var registry = new RouteRegistry(RouteGraphSnapshot.Create([Route("home", "home")]));
        var resolver = new PluginResolver();
        var lease = registry.AddContribution(new RouteContribution(
            "plugin.profile",
            [Route("plugin-profile", "plugin/profile", "plugin.profile", [typeof(PluginResolver)])],
            type => type == typeof(PluginResolver) ? resolver : null));
        var scope = new NavigationScope(registry, _ => throw new InvalidOperationException("Host resolver must not be used."));

        var result = await scope.NavigateByPathAsync("plugin/profile");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal(1, resolver.InvocationCount);
        lease.Dispose();
        Assert.Equal(NavigationResultStatus.NotFound, (await scope.NavigateByPathAsync("plugin/profile")).Status);
    }

    [Fact]
    public async Task ExtensionPointContributionInheritsHostRouteHierarchy()
    {
        var shell = new RouteDescriptor(
            "settings",
            RouteDefinitionKind.Layout,
            "settings",
            new ViewModelTargetDescriptor(typeof(TestViewModel)));
        var extension = new RouteDescriptor(
            "settings.pages",
            RouteDefinitionKind.ExtensionPoint,
            null,
            null,
            parentRouteId: "settings",
            extensionPoint: "settings.pages");
        var registry = new RouteRegistry(RouteGraphSnapshot.Create([shell, extension]));
        using var lease = registry.AddContribution(
            "plugin.profile",
            [new RouteDescriptor(
                "plugin-profile",
                RouteDefinitionKind.Route,
                "profile",
                new ViewModelTargetDescriptor(typeof(TestViewModel)),
                extensionPoint: "settings.pages",
                contributionId: "plugin.profile")]);
        var scope = new NavigationScope(registry);

        var result = await scope.NavigateByPathAsync("settings/profile");

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("settings", registry.CurrentSnapshot.GetRequiredRoute("plugin-profile").ParentRouteId);
    }

    [Fact]
    public void FailedLeaseReleaseCanBeRetriedAfterDependentContributionIsRemoved()
    {
        var registry = new RouteRegistry();
        var hostLease = registry.AddContribution(
            "module.settings",
            [
                new RouteDescriptor(
                    "settings",
                    RouteDefinitionKind.Layout,
                    "settings",
                    new ViewModelTargetDescriptor(typeof(TestViewModel)),
                    contributionId: "module.settings"),
                new RouteDescriptor(
                    "settings.pages",
                    RouteDefinitionKind.ExtensionPoint,
                    null,
                    null,
                    parentRouteId: "settings",
                    extensionPoint: "settings.pages",
                    contributionId: "module.settings"),
            ]);
        var pluginLease = registry.AddContribution(
            "plugin.profile",
            [new RouteDescriptor(
                "plugin-profile",
                RouteDefinitionKind.Route,
                "profile",
                new ViewModelTargetDescriptor(typeof(TestViewModel)),
                extensionPoint: "settings.pages",
                contributionId: "plugin.profile")]);

        Assert.Throws<RouteGraphException>(() => hostLease.Dispose());
        pluginLease.Dispose();
        hostLease.Dispose();

        Assert.Empty(registry.CurrentSnapshot.Routes);
    }

    private static RouteDescriptor Route(
        string id,
        string template,
        string? contributionId = null,
        IReadOnlyList<Type>? resolverTypes = null)
    {
        return new RouteDescriptor(
            id,
            RouteDefinitionKind.Route,
            template,
            new ViewModelTargetDescriptor(typeof(TestViewModel)),
            contributionId: contributionId,
            resolverTypes: resolverTypes);
    }

    private sealed class PluginResolver : IRouteResolver
    {
        public int InvocationCount { get; private set; }

        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            return ValueTask.FromResult(RouteResolveResult.Success());
        }
    }

    private sealed class TestViewModel;
}
