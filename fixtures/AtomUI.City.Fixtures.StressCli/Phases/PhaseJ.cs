using System.Collections.Concurrent;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Localization;
using AtomUI.City.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>Localization 功能矩阵：资源、作用域、事务边界、撤销和诊断。</summary>
public static class PhaseJ
{
    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);

        var services = host.Services;
        var localization = services.GetRequiredService<ILocalizationService>();
        var registry = services.GetRequiredService<LanguagePackageRegistry>();
        var provider = services.GetRequiredService<StressLanguagePackageProvider>();
        var bridge = services.GetRequiredService<StressPresentationLocalizationBridge>();
        var diagnostics = (InMemoryLocalizationDiagnostics)services.GetRequiredService<ILocalizationDiagnostics>();

        var descriptors = registry.Descriptors;
        var distinctKeys = descriptors
            .SelectMany(descriptor => descriptor.InMemoryResources?.Keys ?? [])
            .Distinct(StringComparer.Ordinal)
            .Count();
        var resourceEntries = descriptors.Sum(descriptor => descriptor.InMemoryResources?.Count ?? 0);
        var scopes = descriptors.Select(descriptor => descriptor.Scope).ToHashSet();
        var catalogOk = descriptors.Count == StressLocalizationCatalog.DescriptorCount
            && distinctKeys >= StressLocalizationCatalog.MinimumDistinctKeyCount
            && resourceEntries >= StressLocalizationCatalog.MinimumResourceEntryCount
            && Enum.GetValues<ResourceScope>().All(scopes.Contains);

        var unloadedAtStart = localization.CultureState.Value.LoadedPackageIds.Count == 0
            && provider.TotalLoadCount == 0;
        var globalSave = await localization.GetStringAsync("Common.Save", cancellationToken).ConfigureAwait(false);
        var presentationTheme = await localization.GetStringAsync("Presentation.Theme", cancellationToken).ConfigureAwait(false);
        var scopedPackageIds = descriptors
            .Where(descriptor => descriptor.Scope is not ResourceScope.Host and not ResourceScope.Presentation)
            .Select(descriptor => descriptor.PackageId)
            .ToHashSet(StringComparer.Ordinal);
        var lazyStartupOk = unloadedAtStart
            && globalSave.Value == "Save"
            && presentationTheme.Value == "System theme"
            && provider.GetLoadCount("en-US", "Host.Core") == 1
            && provider.GetLoadCount("en-US", "Presentation.Shell") == 1
            && descriptors
                .Where(descriptor => scopedPackageIds.Contains(descriptor.PackageId))
                .All(descriptor => provider.GetLoadCount(descriptor.Culture.Name, descriptor.PackageId) == 0);
        var lazyLoadCount = provider.TotalLoadCount;
        var lazyLoadedPackageCount = localization.CultureState.Value.LoadedPackageIds.Count;

        var operationsContext = new LocalizationLookupContext(moduleId: StressLocalizationCatalog.OperationsModuleId);
        var billingContext = new LocalizationLookupContext(moduleId: StressLocalizationCatalog.BillingModuleId);
        var supportModuleContext = new LocalizationLookupContext(moduleId: StressLocalizationCatalog.SupportModuleId);
        var ordersContext = new LocalizationLookupContext(routeId: StressLocalizationCatalog.OrdersRouteId);
        var paymentsContext = new LocalizationLookupContext(routeId: StressLocalizationCatalog.PaymentsRouteId);
        var searchContext = new LocalizationLookupContext(routeId: StressLocalizationCatalog.SearchRouteId);
        var supportRouteContext = new LocalizationLookupContext(routeId: StressLocalizationCatalog.SupportRouteId);
        var reportsContext = new LocalizationLookupContext(routeId: StressLocalizationCatalog.ReportsRouteId);
        var mainWindowContext = new LocalizationLookupContext(windowId: StressLocalizationCatalog.MainWindowId);
        var exportWindowContext = new LocalizationLookupContext(windowId: StressLocalizationCatalog.ExportWindowId);
        var salesPluginContext = new LocalizationLookupContext(pluginId: StressLocalizationCatalog.SalesPluginId);
        var combinedContext = new LocalizationLookupContext(
            moduleId: StressLocalizationCatalog.OperationsModuleId,
            pluginId: StressLocalizationCatalog.SalesPluginId,
            routeId: StressLocalizationCatalog.OrdersRouteId,
            windowId: StressLocalizationCatalog.MainWindowId);

        var leases = new List<ILocalizationScopeLease>
        {
            localization.ActivateScope(operationsContext),
            localization.ActivateScope(billingContext),
            localization.ActivateScope(supportModuleContext),
            localization.ActivateScope(ordersContext),
            localization.ActivateScope(paymentsContext),
            localization.ActivateScope(searchContext),
            localization.ActivateScope(supportRouteContext),
            localization.ActivateScope(mainWindowContext),
            localization.ActivateScope(exportWindowContext),
            localization.ActivateScope(salesPluginContext),
        };

        var moduleSave = await localization.GetStringAsync("Common.Save", operationsContext, cancellationToken).ConfigureAwait(false);
        var windowSave = await localization.GetStringAsync(
            "Common.Save",
            new LocalizationLookupContext(
                moduleId: StressLocalizationCatalog.OperationsModuleId,
                windowId: StressLocalizationCatalog.MainWindowId),
            cancellationToken).ConfigureAwait(false);
        var routeSave = await localization.GetStringAsync("Common.Save", combinedContext, cancellationToken).ConfigureAwait(false);
        var pluginExport = await localization.GetStringAsync(
            "Common.Export",
            new LocalizationLookupContext(
                moduleId: StressLocalizationCatalog.OperationsModuleId,
                pluginId: StressLocalizationCatalog.SalesPluginId),
            cancellationToken).ConfigureAwait(false);
        var inactiveScopeSave = await localization.GetStringAsync(
            "Common.Save",
            new LocalizationLookupContext(moduleId: "fixtures.module.inactive"),
            cancellationToken).ConfigureAwait(false);
        var scopePriorityOk = globalSave.Value == "Save"
            && moduleSave.Value == "Save operation"
            && windowSave.Value == "Save workspace"
            && routeSave.Value == "Save orders"
            && pluginExport.Value == "Export sales report"
            && inactiveScopeSave.Value == "Save";

        var ordersMarker = await localization.GetStringAsync("Route.ContextMarker", ordersContext, cancellationToken).ConfigureAwait(false);
        var paymentsMarker = await localization.GetStringAsync("Route.ContextMarker", paymentsContext, cancellationToken).ConfigureAwait(false);
        var contextIsolationOk = ordersMarker.Value == "Orders route"
            && paymentsMarker.Value == "Payments route"
            && ordersMarker.Value != paymentsMarker.Value;

        var englishFallback = await localization.GetStringAsync(
            "Operations.LegacyHint",
            operationsContext,
            cancellationToken).ConfigureAwait(false);
        var fallbackOk = englishFallback.IsFallback
            && englishFallback.Culture.Name == "en"
            && englishFallback.Value == "Open the legacy operations panel.";

        var texts = new List<ILocalizedText>
        {
            await localization.CreateTextAsync("Common.Save", cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("Operations.Title", operationsContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("Orders.Title", ordersContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("MainWindow.Title", mainWindowContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateTextAsync("SalesPlugin.Banner", salesPluginContext, cancellationToken).ConfigureAwait(false),
            await localization.CreateMessageTextAsync("Billing.Amount", [1234.5m], billingContext, cancellationToken).ConfigureAwait(false),
        };
        var textChanges = new ConcurrentDictionary<ILocalizedText, int>();
        foreach (var text in texts)
        {
            textChanges[text] = 0;
            text.Changed += (_, _) => textChanges.AddOrUpdate(text, 1, static (_, count) => count + 1);
        }

        provider.ResetCounters();
        provider.DelayMilliseconds = 20;
        leases.Add(localization.ActivateScope(reportsContext));
        var concurrentLookups = await Task.WhenAll(
                Enumerable.Range(0, 64)
                    .Select(_ => localization.GetStringAsync("Reports.Title", reportsContext, cancellationToken).AsTask()))
            .ConfigureAwait(false);
        provider.DelayMilliseconds = 0;
        var concurrentLoadOk = concurrentLookups.All(result => result.Value == "Reports")
            && provider.GetLoadCount("en-US", "Route.Reports") == 1;
        var reportLoadCount = provider.GetLoadCount("en-US", "Route.Reports");

        var cultureBeforeFailures = localization.CurrentCulture.Name;
        var revisionBeforeFailures = localization.CultureRevision;
        provider.FailNext("Host.Core");
        var failedSwitch = await localization.SetCultureAsync("zh-CN", cancellationToken).ConfigureAwait(false);

        provider.DelayMilliseconds = 50;
        using var preCommitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        preCommitCancellation.CancelAfter(TimeSpan.FromMilliseconds(5));
        var cancelledSwitch = await localization.SetCultureAsync("zh-CN", preCommitCancellation.Token).ConfigureAwait(false);
        provider.DelayMilliseconds = 0;
        var preCommitOk = !failedSwitch.Succeeded
            && failedSwitch.Error?.Kind == LocalizationErrorKind.PackageLoadFailed
            && !cancelledSwitch.Succeeded
            && cancelledSwitch.Error?.Kind == LocalizationErrorKind.Cancelled
            && localization.CurrentCulture.Name == cultureBeforeFailures
            && localization.CultureRevision == revisionBeforeFailures
            && textChanges.Values.All(count => count == 0);

        using var postCommitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bridge.CancelCallerOnNextApply(postCommitCancellation);
        var chineseSwitch = await localization.SetCultureAsync("zh-CN", postCommitCancellation.Token).ConfigureAwait(false);
        var postCommitBridgeApplyCount = bridge.ApplyCount;
        var chineseFallback = await localization.GetStringAsync(
            "Operations.LegacyHint",
            operationsContext,
            cancellationToken).ConfigureAwait(false);
        fallbackOk = fallbackOk
            && chineseFallback.IsFallback
            && chineseFallback.Culture.Name == "zh-Hans"
            && chineseFallback.Value == "打开旧版运营面板。";
        var postCommitOk = chineseSwitch.Succeeded
            && postCommitCancellation.IsCancellationRequested
            && localization.CurrentCulture.Name == "zh-CN"
            && texts[0].Value == "保存"
            && texts[1].Value == "运营中心"
            && texts[2].Value == "订单"
            && texts[3].Value == "AtomUI City 运营控制台"
            && texts[4].Value == "销售扩展已启用。"
            && texts[5].Value.Contains("1,234.50", StringComparison.Ordinal)
            && textChanges.Values.All(count => count == 1);

        bridge.FailNext();
        var bridgeFailedSwitch = await localization.SetCultureAsync("en-US", cancellationToken).ConfigureAwait(false);
        var bridgeFailureOk = !bridgeFailedSwitch.Succeeded
            && bridgeFailedSwitch.Error?.Kind == LocalizationErrorKind.PresentationApplyFailed
            && localization.CurrentCulture.Name == "en-US"
            && texts[0].Value == "Save"
            && texts[1].Value == "Operations center"
            && texts[2].Value == "Orders"
            && texts[3].Value == "AtomUI City Operations"
            && texts[4].Value == "Sales extension is active."
            && textChanges.Values.All(count => count == 2);
        var bridgeFailureCulture = localization.CurrentCulture.Name;
        var dynamicChangeSnapshot = textChanges.Values.ToArray();
        var dynamicRevisionAligned = texts.All(text => text.Revision == localization.CultureRevision);
        var dynamicTextOk = postCommitOk
            && bridgeFailureOk
            && dynamicRevisionAligned;

        var formatted = await localization.GetMessageAsync(
            "Billing.PaymentReceived",
            [1234.5m, "Contoso"],
            billingContext,
            cancellationToken).ConfigureAwait(false);
        var formatFailed = await localization.GetMessageAsync(
            "Billing.Amount",
            [new ThrowingFormattable()],
            billingContext,
            cancellationToken).ConfigureAwait(false);
        var missing = await localization.GetStringAsync(
            "Missing.Business.CriticalLabel",
            cancellationToken).ConfigureAwait(false);

        var pluginBeforeRevoke = await localization.GetStringAsync(
            "Common.Export",
            salesPluginContext,
            cancellationToken).ConfigureAwait(false);
        var pluginTextChangesBefore = textChanges[texts[4]];
        var revokedCount = await localization.RevokePackagesByContributionIdAsync(
            StressLocalizationCatalog.SalesContributionId,
            cancellationToken).ConfigureAwait(false);
        var pluginAfterRevoke = await localization.GetStringAsync(
            "Common.Export",
            salesPluginContext,
            cancellationToken).ConfigureAwait(false);
        var secondRevokeCount = await localization.RevokePackagesByContributionIdAsync(
            StressLocalizationCatalog.SalesContributionId,
            cancellationToken).ConfigureAwait(false);
        var pluginRevokeOk = pluginBeforeRevoke.Value == "Export sales report"
            && revokedCount == 2
            && pluginAfterRevoke.Value == "Export"
            && secondRevokeCount == 0
            && texts[4].IsMissing
            && textChanges[texts[4]] == pluginTextChangesBefore + 1
            && registry.Descriptors.All(descriptor => descriptor.ContributionId != StressLocalizationCatalog.SalesContributionId);

        var changesBeforeRelease = textChanges.Values.Sum();
        foreach (var text in texts)
        {
            text.Dispose();
        }

        for (var index = leases.Count - 1; index >= 0; index--)
        {
            leases[index].Dispose();
        }

        var finalSwitch = await localization.SetCultureAsync("zh-CN", cancellationToken).ConfigureAwait(false);
        var releasedScopeLookup = await localization.GetStringAsync(
            "Operations.Title",
            operationsContext,
            cancellationToken).ConfigureAwait(false);
        var records = diagnostics.Records;
        var requiredDiagnosticIds = new[]
        {
            LocalizationDiagnosticIds.ResourceMissing,
            LocalizationDiagnosticIds.PackageLoadFailed,
            LocalizationDiagnosticIds.AtomUiApplyFailed,
            LocalizationDiagnosticIds.MessageFormatFailed,
            LocalizationDiagnosticIds.CultureChanged,
            LocalizationDiagnosticIds.CultureSwitchRejected,
            LocalizationDiagnosticIds.PluginPackagesRevoked,
        };
        var diagnosticsAndReleaseOk = finalSwitch.Succeeded
            && textChanges.Values.Sum() == changesBeforeRelease
            && releasedScopeLookup.IsMissing
            && missing.IsMissing
            && missing.Value == "!Missing.Business.CriticalLabel!"
            && formatted.Value.Contains("Contoso", StringComparison.Ordinal)
            && !formatted.IsFormatFailed
            && formatFailed.IsFormatFailed
            && requiredDiagnosticIds.All(code => records.Any(record => record.Code == code))
            && records.All(record => !string.IsNullOrWhiteSpace(record.OperationId));

        FixtureState.Report.Record(
            "I19-localization-catalog",
            "Localization catalog 覆盖 30 包、120+ key、250+ 资源和六类 scope",
            catalogOk,
            $"descriptors={descriptors.Count} keys={distinctKeys} entries={resourceEntries} scopes={scopes.Count}");
        FixtureState.Report.Record(
            "I20-localization-lazy-start",
            "Host 启动不加载资源，首次全局查找不触碰 scoped package",
            lazyStartupOk,
            $"loads={lazyLoadCount} loadedPackages={lazyLoadedPackageCount}");
        FixtureState.Report.Record(
            "I21-localization-scope-priority",
            "Route > Window > Plugin > Module > Host > Presentation 优先级确定",
            scopePriorityOk);
        FixtureState.Report.Record(
            "I22-localization-context",
            "并行活动 Route 的相同 key 按 lookup context 隔离",
            contextIsolationOk,
            $"orders={ordersMarker.Value} payments={paymentsMarker.Value}");
        FixtureState.Report.Record(
            "I23-localization-fallback",
            "en-US/en 与 zh-CN/zh-Hans scoped fallback 均可递归命中",
            fallbackOk);
        FixtureState.Report.Record(
            "I24-localization-dynamic-text",
            "普通和格式化动态文案在 culture commit 后完整刷新",
            dynamicTextOk,
            $"changes={string.Join(',', dynamicChangeSnapshot)} revisionAligned={dynamicRevisionAligned}");
        FixtureState.Report.Record(
            "I25-localization-concurrent-load",
            "64 个首次并发 lookup 合并为一次 provider load",
            concurrentLoadOk,
            $"reportLoads={reportLoadCount}");
        FixtureState.Report.Record(
            "I26-localization-precommit",
            "加载失败与调用方取消均不提交部分 culture 状态",
            preCommitOk,
            $"failure={failedSwitch.Error?.Kind} cancellation={cancelledSwitch.Error?.Kind}");
        FixtureState.Report.Record(
            "I27-localization-postcommit",
            "commit 后调用方取消不阻断 bridge 和动态文案刷新",
            postCommitOk,
            $"bridgeApply={postCommitBridgeApplyCount}");
        FixtureState.Report.Record(
            "I28-localization-bridge-failure",
            "Presentation apply 失败不回滚 culture 且继续刷新文案",
            bridgeFailureOk,
            $"result={bridgeFailedSwitch.Error?.Kind} culture={bridgeFailureCulture}");
        FixtureState.Report.Record(
            "I29-localization-plugin-revoke",
            "插件 contribution 撤销清理双 culture 包并刷新存量文本",
            pluginRevokeOk,
            $"revoked={revokedCount} second={secondRevokeCount}");
        FixtureState.Report.Record(
            "I30-localization-diagnostics-release",
            "故障诊断字段完整，text/lease 释放后无回调且 scoped 资源不可见",
            diagnosticsAndReleaseOk,
            $"diagnostics={records.Count} releasedLookupMissing={releasedScopeLookup.IsMissing}");

        await host.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class ThrowingFormattable : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            throw new FormatException("Injected formatter failure.");
        }

        public override string ToString()
        {
            throw new FormatException("Injected formatter failure.");
        }
    }
}
