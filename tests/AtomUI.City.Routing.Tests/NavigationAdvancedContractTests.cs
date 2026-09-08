namespace AtomUI.City.Routing.Tests;

public sealed class NavigationAdvancedContractTests
{
    [Fact]
    public async Task DeepLinkMatchesPathAndMergesDecodedQueryParameters()
    {
        var scope = new NavigationScope(RouteGraphSnapshot.Create(
            [Route("profile", "profile/{id:int}")]));

        var result = await scope.NavigateByUriAsync(
            new Uri("atomui://boxer/profile/42?tab=activity%20feed#summary"));

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("42", result.Parameters["id"]);
        Assert.Equal("activity feed", result.Parameters["tab"]);
        Assert.Equal("summary", result.Parameters["fragment"]);
    }

    [Fact]
    public async Task BackRestoresResolvedDataWithoutRunningResolverAgain()
    {
        var resolver = new CountingResolver();
        var graph = RouteGraphSnapshot.Create(
        [
            Route("home", "home"),
            Route("profile", "profile", resolverTypes: [typeof(CountingResolver)]),
            Route("settings", "settings"),
        ]);
        var scope = new NavigationScope(
            graph,
            type => type == typeof(CountingResolver) ? resolver : null);

        await scope.NavigateByPathAsync("home");
        await scope.NavigateByPathAsync("profile");
        await scope.NavigateByPathAsync("settings");
        var result = await scope.BackAsync();

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("profile", scope.CurrentSnapshot.Route.RouteId);
        Assert.Equal(1, resolver.InvocationCount);
        Assert.Equal(1, scope.CurrentSnapshot.ResolvedData["generation"]);
    }

    [Fact]
    public async Task BackSkipsJournalEntriesOwnedByUnloadedContribution()
    {
        var registry = new RouteRegistry(RouteGraphSnapshot.Create(
            [Route("home", "home"), Route("settings", "settings")]));
        using var lease = registry.AddContribution(
            "plugin.profile",
            [Route("plugin.profile", "plugin", contributionId: "plugin.profile")]);
        var scope = new NavigationScope(registry);

        await scope.NavigateByPathAsync("home");
        await scope.NavigateByPathAsync("plugin");
        await scope.NavigateByPathAsync("settings");
        lease.Dispose();

        var result = await scope.BackAsync();

        Assert.Equal(NavigationResultStatus.Success, result.Status);
        Assert.Equal("home", scope.CurrentSnapshot.Route.RouteId);
    }

    [Fact]
    public async Task StaticRedirectCarriesMatchedPathParametersToTargetRoute()
    {
        var graph = RouteGraphSnapshot.Create(
        [
            new RouteDescriptor(
                "legacy.profile",
                RouteDefinitionKind.Redirect,
                "users/{id:int}",
                viewModelTarget: null,
                redirectTargetRouteId: "profile"),
            Route("profile", "profiles/{id:int}"),
        ]);
        var scope = new NavigationScope(graph);

        var result = await scope.NavigateByPathAsync("users/42");

        Assert.Equal(NavigationResultStatus.Redirected, result.Status);
        Assert.Equal("profile", result.Route.RouteId);
        Assert.Equal("42", result.Parameters["id"]);
    }

    [Theory]
    [InlineData((NavigationMode)999, NavigationHistoryBehavior.Record, NavigationConcurrencyPolicy.Queue)]
    [InlineData(NavigationMode.Push, (NavigationHistoryBehavior)999, NavigationConcurrencyPolicy.Queue)]
    [InlineData(NavigationMode.Push, NavigationHistoryBehavior.Record, (NavigationConcurrencyPolicy)999)]
    public async Task UnknownNavigationOptionValuesAreRejected(
        NavigationMode mode,
        NavigationHistoryBehavior history,
        NavigationConcurrencyPolicy concurrency)
    {
        var scope = new NavigationScope(RouteGraphSnapshot.Create([Route("home", "home")]));

        var result = await scope.NavigateByPathAsync(
            "home",
            new NavigationOptions
            {
                Mode = mode,
                HistoryBehavior = history,
                ConcurrencyPolicy = concurrency,
            });

        Assert.Equal(NavigationResultStatus.Rejected, result.Status);
        Assert.Equal("CITY-NAVIGATION-OPTIONS-INVALID", result.Error?.Code);
    }

    private static RouteDescriptor Route(
        string id,
        string template,
        string? contributionId = null,
        IReadOnlyList<Type>? resolverTypes = null) =>
        new(
            id,
            RouteDefinitionKind.Route,
            template,
            new ViewModelTargetDescriptor(typeof(TestViewModel)),
            contributionId: contributionId,
            resolverTypes: resolverTypes);

    private sealed class CountingResolver : IRouteResolver
    {
        public int InvocationCount { get; private set; }

        public ValueTask<RouteResolveResult> ResolveAsync(
            RouteResolveContext context,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            return ValueTask.FromResult(RouteResolveResult.Success(
                new Dictionary<string, object?> { ["generation"] = InvocationCount }));
        }
    }

    private sealed class TestViewModel;
}
