using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Routing;
using AtomUI.City.Fixtures.StressCli.Services;
using AtomUI.City.Fixtures.StressCli.ViewModels;
using AtomUI.City.Mvvm;
using AtomUI.City.Routing;
using AtomUI.City.State;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>Phase H：generated routes、管道、历史、并发策略、动态贡献和 MVVM 目标。</summary>
public static class PhaseH
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken);

        var registry = host.Services.GetRequiredService<IRouteRegistry>();
        var initialGraph = registry.CurrentSnapshot;
        var idsAreStable = initialGraph.Routes.Select(route => route.RouteId).Distinct(StringComparer.Ordinal).Count() == 31 &&
                           initialGraph.Routes.All(route => route.RouteId.StartsWith("fixtures.routes.", StringComparison.Ordinal));
        FixtureState.Report.Record(
            "I12-manifest",
            "RouteMap generator manifest 恰好生成 31 条稳定路由",
            initialGraph.Routes.Count == StressCliProgram.RouteCount && idsAreStable,
            $"routes={initialGraph.Routes.Count} version={initialGraph.Version}");

        using var navigationServices = host.Services.CreateScope();
        var router = navigationServices.ServiceProvider.GetRequiredService<IRouter>();
        var navigationScope = (NavigationScope)router;
        var audit = host.Services.GetRequiredService<INavigationAudit>();

        var order = await router.NavigateAsync(
            FixtureRoutes.OrderDetails(),
            new OrderRouteParameters(7),
            cancellationToken: cancellationToken);
        var orderResolved = order.Status == NavigationResultStatus.Success &&
                            order.Parameters.TryGetValue("id", out var id) && id == "7" &&
                            navigationScope.CurrentSnapshot.ResolvedData.TryGetValue("order", out var resolved) &&
                            Equals(resolved, "resolved-order:7");

        var blocked = await router.NavigateAsync(
            FixtureRoutes.OrderDetails(),
            new OrderRouteParameters(13),
            new NavigationOptions { ForceReload = true },
            cancellationToken: cancellationToken);
        var product = await router.NavigateAsync(
            FixtureRoutes.ProductDetails(),
            new ProductRouteParameters("SKU-0001"),
            cancellationToken: cancellationToken);
        var invalidProduct = await router.NavigateByPathAsync("catalog/products/invalid", cancellationToken: cancellationToken);
        var normalSearch = await router.NavigateByPathAsync("search/regular", cancellationToken: cancellationToken);
        var premiumSearch = await router.NavigateByPathAsync("search/vip-customer", cancellationToken: cancellationToken);

        var matchingOk = orderResolved &&
                         blocked.Status == NavigationResultStatus.Rejected &&
                         blocked.Error?.Code == "FIXTURE-ORDER-BLOCKED" &&
                         product.Status == NavigationResultStatus.Success &&
                         invalidProduct.Status == NavigationResultStatus.NotFound &&
                         normalSearch.ActiveRoute?.RouteId == "fixtures.routes.search" &&
                         premiumSearch.ActiveRoute?.RouteId == "fixtures.routes.premium-search";
        FixtureState.Report.Record(
            "I13-matching",
            "类型参数、regex、同形模板 policy 与 guard 拒绝均生效",
            matchingOk,
            $"order={order.Status} blocked={blocked.Status} product={product.Status} invalid={invalidProduct.Status} normal={normalSearch.ActiveRoute?.RouteId} premium={premiumSearch.ActiveRoute?.RouteId}");

        var routeId = "fixtures.routes.order-details";
        var entries = audit.Entries;
        var policyIndex = IndexOf(entries, $"policy:{routeId}");
        var guardIndex = IndexOf(entries, $"guard-enter:{routeId}");
        var resolverIndex = IndexOf(entries, $"resolver:{routeId}");
        var middlewareBeforeIndex = IndexOf(entries, $"middleware-before:{routeId}");
        var middlewareAfterIndex = IndexOfPrefix(entries, $"middleware-after:{routeId}:");
        var pipelineOk = policyIndex >= 0 && policyIndex < middlewareBeforeIndex && middlewareBeforeIndex < guardIndex &&
                         guardIndex < resolverIndex && resolverIndex < middlewareAfterIndex;
        FixtureState.Report.Record(
            "I13-pipeline",
            "Router pipeline 按 policy→middleware-enter→guard→resolver→middleware-exit 执行",
            pipelineOk,
            $"indexes={policyIndex},{guardIndex},{resolverIndex},{middlewareBeforeIndex},{middlewareAfterIndex}");

        var side = await router.NavigateByPathAsync(
            "notifications",
            new NavigationOptions { OutletName = "side" },
            cancellationToken);
        var legacy = await router.NavigateByPathAsync("legacy-orders", cancellationToken: cancellationToken);
        await router.NavigateAsync(FixtureRoutes.Billing(), cancellationToken: cancellationToken);
        var back = await router.BackAsync(cancellationToken);
        var forward = await router.ForwardAsync(cancellationToken);
        var navigationFeaturesOk = side.ActiveRoute?.OutletName == "side" &&
                                   legacy.ActiveRoute?.RouteId == "fixtures.routes.orders" &&
                                   back.Status == NavigationResultStatus.Success &&
                                   forward.Status == NavigationResultStatus.Success &&
                                   forward.ActiveRoute?.RouteId == "fixtures.routes.billing";
        FixtureState.Report.Record(
            "I14-navigation",
            "命名 outlet、静态 redirect 与 back/forward journal 完整",
            navigationFeaturesOk,
            $"side={side.Status} legacy={legacy.ActiveRoute?.RouteId} back={back.ActiveRoute?.RouteId} forward={forward.ActiveRoute?.RouteId}");

        var concurrencyOk = await VerifyConcurrencyAsync(router, cancellationToken).ConfigureAwait(false);
        FixtureState.Report.Record(
            "I14-concurrency",
            "Queue、CancelPrevious、RejectIfBusy 三种导航并发策略均生效",
            concurrencyOk);

        var versionBeforeContribution = registry.CurrentSnapshot.Version;
        var contribution = new RouteDescriptor(
            "fixtures.routes.plugin-inspector",
            RouteDefinitionKind.Route,
            "plugin-inspector",
            new ViewModelTargetDescriptor(typeof(SupportViewModel)),
            extensionPoint: "fixtures.extensions.operations");
        var lease = registry.AddContribution("fixtures.plugin.operations", [contribution]);
        var contributed = await router.NavigateByPathAsync("operations/plugin-inspector", cancellationToken: cancellationToken);
        var versionWithContribution = registry.CurrentSnapshot.Version;
        lease.Dispose();
        var revoked = await router.NavigateByPathAsync("operations/plugin-inspector", cancellationToken: cancellationToken);
        var contributionOk = contributed.Status == NavigationResultStatus.Success &&
                             versionWithContribution > versionBeforeContribution &&
                             registry.CurrentSnapshot.Version > versionWithContribution &&
                             revoked.Status == NavigationResultStatus.NotFound;
        FixtureState.Report.Record(
            "I15-contribution",
            "动态 contribution attach 后可导航，lease revoke 后立即消失",
            contributionOk,
            $"attach={contributed.Status} revoke={revoked.Status} versions={versionBeforeContribution}->{versionWithContribution}->{registry.CurrentSnapshot.Version}");

        var targetResult = await router.NavigateAsync(FixtureRoutes.Payments(), cancellationToken: cancellationToken);
        var targetType = targetResult.ActiveRoute?.ViewModelTarget?.ViewModelType;
        FixtureViewModelBase? routedViewModel = null;
        if (targetType is not null)
        {
            routedViewModel = (FixtureViewModelBase)ActivatorUtilities.CreateInstance(
                navigationServices.ServiceProvider,
                targetType);
            host.Services.GetRequiredService<IStateRegistry>().Add(StateDefinition.Create(
                routedViewModel.StateKey,
                0,
                access: StateAccessPolicy.HostWrite));
            await routedViewModel.ActivateAsync(new ActivationScope(), cancellationToken).ConfigureAwait(false);
            await routedViewModel.RefreshCommand.ExecuteAsync(null).ConfigureAwait(false);
            await routedViewModel.DeactivateAsync(cancellationToken).ConfigureAwait(false);
        }

        var routedVmOk = routedViewModel is PaymentsViewModel &&
                         routedViewModel.CountOf("refresh") == 1 &&
                         routedViewModel.ActivationState == ActivationState.Deactivated;
        FixtureState.Report.Record(
            "I10-routed-vm",
            "Router 目标由 DI 构造，完成 MVVM 激活、命令和停用",
            routedVmOk,
            $"type={routedViewModel?.GetType().Name} refresh={routedViewModel?.CountOf("refresh")}");
        routedViewModel?.Dispose();

        await host.StopAsync(cancellationToken);
    }

    private static async Task<bool> VerifyConcurrencyAsync(IRouter router, CancellationToken cancellationToken)
    {
        var rejectOptions = new NavigationOptions
        {
            ConcurrencyPolicy = NavigationConcurrencyPolicy.RejectIfBusy,
            HistoryBehavior = NavigationHistoryBehavior.Skip,
        };
        var rejectFirst = router.NavigateAsync(FixtureRoutes.Workflow(), rejectOptions, cancellationToken).AsTask();
        await Task.Delay(5, cancellationToken).ConfigureAwait(false);
        var rejectSecond = await router.NavigateAsync(FixtureRoutes.Workflow(), rejectOptions, cancellationToken);
        var rejectFirstResult = await rejectFirst.ConfigureAwait(false);

        var queueOptions = new NavigationOptions
        {
            ConcurrencyPolicy = NavigationConcurrencyPolicy.Queue,
            HistoryBehavior = NavigationHistoryBehavior.Skip,
        };
        var queued = Enumerable.Range(0, 3)
            .Select(_ => router.NavigateAsync(FixtureRoutes.Workflow(), queueOptions, cancellationToken).AsTask())
            .ToArray();
        var queueResults = await Task.WhenAll(queued).ConfigureAwait(false);

        var cancelOptions = new NavigationOptions
        {
            ConcurrencyPolicy = NavigationConcurrencyPolicy.CancelPrevious,
            HistoryBehavior = NavigationHistoryBehavior.Skip,
        };
        var cancelled = router.NavigateAsync(FixtureRoutes.Workflow(), cancelOptions, cancellationToken).AsTask();
        await Task.Delay(5, cancellationToken).ConfigureAwait(false);
        var replacement = router.NavigateAsync(FixtureRoutes.Workflow(), cancelOptions, cancellationToken).AsTask();
        var cancelResults = await Task.WhenAll(cancelled, replacement).ConfigureAwait(false);

        return rejectFirstResult.Status == NavigationResultStatus.Success &&
               rejectSecond.Status == NavigationResultStatus.Rejected &&
               queueResults.All(result => result.Status == NavigationResultStatus.Success) &&
               cancelResults[0].Status == NavigationResultStatus.Cancelled &&
               cancelResults[1].Status == NavigationResultStatus.Success;
    }

    private static int IndexOf(IReadOnlyList<string> entries, string expected)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index], expected, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOfPrefix(IReadOnlyList<string> entries, string prefix)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].StartsWith(prefix, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
