using System.Globalization;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Localization;
using AtomUI.City.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>反复创建、运行和释放完整 Host，验证 Localization 最终收束。</summary>
public static class PhaseO
{
    public static async Task RunAsync(
        StressExecutionOptions options,
        CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        var cycleResults = new List<HostCycleResult>(options.HostCycles);
        for (var cycle = 0; cycle < options.HostCycles; cycle++)
        {
            cycleResults.Add(await RunHostCycleAsync(cycle, cancellationToken).ConfigureAwait(false));
        }

        var pendingDisposeResult = await RunPendingLoadDisposeAsync(cancellationToken).ConfigureAwait(false);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }

        var successfulCycles = cycleResults.Count(result => result.BusinessFlowPassed);
        var idempotentCycles = cycleResults.Count(result => result.IdempotentShutdownPassed);
        var postDisposeCycles = cycleResults.Count(result => result.PostDisposePassed);
        var weakReferences = cycleResults
            .SelectMany(result => result.WeakReferences)
            .ToArray();
        var collectedReferences = weakReferences.Count(reference => !reference.IsAlive);

        FixtureState.Report.Record(
            "O01-localization-host-cycles",
            "完整 City Host 反复启动并完成多文化 scoped 业务流",
            successfulCycles == options.HostCycles,
            $"passes={successfulCycles}/{options.HostCycles}");
        FixtureState.Report.Record(
            "O02-localization-host-shutdown",
            "并发 Stop/Dispose 调用幂等完成且 Provider 无在途工作",
            idempotentCycles == options.HostCycles,
            $"passes={idempotentCycles}/{options.HostCycles}");
        FixtureState.Report.Record(
            "O03-localization-pending-load-dispose",
            "Host Dispose 等待忽略取消的在途 package load，释放 gate 后完整收束",
            pendingDisposeResult,
            $"pending={pendingDisposeResult}");
        FixtureState.Report.Record(
            "O04-localization-release-collection",
            "Dispose 后 API 合同稳定且 Host/Service/Text 不被静态订阅持有",
            postDisposeCycles == options.HostCycles && collectedReferences == weakReferences.Length,
            $"postDispose={postDisposeCycles}/{options.HostCycles} collected={collectedReferences}/{weakReferences.Length}");
    }

    private static async Task<HostCycleResult> RunHostCycleAsync(
        int cycle,
        CancellationToken cancellationToken)
    {
        IApplicationHost? host = StressHost.CreateBuilder().Build();
        try
        {
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            var localization = host.Services.GetRequiredService<ILocalizationService>();
            var provider = host.Services.GetRequiredService<StressLanguagePackageProvider>();
            var context = new LocalizationLookupContext(moduleId: StressLocalizationCatalog.OperationsModuleId);
            var lease = localization.ActivateScope(context);
            var key = StressLocalizationCatalog.GetExtendedKey("Module.Operations", 0);
            var text = await localization.CreateTextAsync(key, context, cancellationToken)
                .ConfigureAwait(false);
            var callbackCount = 0;
            text.Changed += (_, _) => Interlocked.Increment(ref callbackCount);
            var targetCulture = cycle % 2 == 0 ? "fr-FR" : "de-DE";
            var switchResult = await localization.SetCultureAsync(targetCulture, cancellationToken).ConfigureAwait(false);
            var title = await localization.GetStringAsync(key, context, cancellationToken)
                .ConfigureAwait(false);
            var expectedCulture = StressLocalizationCatalog.ExtendedLeafContainsKey(targetCulture, 0)
                ? targetCulture
                : targetCulture == "fr-FR" ? "fr" : "zh-Hant";
            var expected = StressLocalizationCatalog.GetExtendedValue("Module.Operations", expectedCulture, 0);
            var businessFlowPassed = switchResult.Succeeded
                && title.Value == expected
                && text.Value == expected
                && callbackCount == 1;

            if (cycle % 2 == 0)
            {
                text.Dispose();
                lease.Dispose();
            }

            var stopTasks = Enumerable.Range(0, 8)
                .Select(_ => host.StopAsync(cancellationToken))
                .ToArray();
            await Task.WhenAll(stopTasks).ConfigureAwait(false);
            var firstDispose = host.DisposeAsync().AsTask();
            var secondDispose = host.DisposeAsync().AsTask();
            await Task.WhenAll(firstDispose, secondDispose).ConfigureAwait(false);

            var callbacksAfterDispose = callbackCount;
            await text.RefreshAsync(cancellationToken).ConfigureAwait(false);
            var postDisposePassed = callbackCount == callbacksAfterDispose
                && ThrowsObjectDisposed(() => localization.ActivateScope(context))
                && await ThrowsObjectDisposedAsync(
                    () => localization.GetStringAsync("Common.Save", cancellationToken).AsTask())
                    .ConfigureAwait(false)
                && await ThrowsObjectDisposedAsync(
                    () => localization.SetCultureAsync("en-US", cancellationToken).AsTask())
                    .ConfigureAwait(false);
            lease.Dispose();
            text.Dispose();

            var weakReferences = new[]
            {
                new WeakReference(host),
                new WeakReference(localization),
                new WeakReference(text),
            };
            var idempotentShutdownPassed = stopTasks.All(task => task.IsCompletedSuccessfully)
                && firstDispose.IsCompletedSuccessfully
                && secondDispose.IsCompletedSuccessfully
                && provider.ActiveLoadCount == 0
                && provider.CompletedLoadCount > 0;

            host = null;
            return new HostCycleResult(
                businessFlowPassed,
                idempotentShutdownPassed,
                postDisposePassed,
                weakReferences);
        }
        finally
        {
            if (host is not null)
            {
                await host.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<bool> RunPendingLoadDisposeAsync(CancellationToken cancellationToken)
    {
        var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);
        var localization = host.Services.GetRequiredService<ILocalizationService>();
        var registry = host.Services.GetRequiredService<LanguagePackageRegistry>();
        var provider = host.Services.GetRequiredService<StressLanguagePackageProvider>();
        var packageId = "Lifecycle.Pending";
        var scopeId = "fixtures.plugin.lifecycle.pending";
        var key = "Lifecycle.Pending.Key";
        var descriptor = new LanguagePackageDescriptor(
            packageId,
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Plugin)
        {
            ScopeId = scopeId,
            ProviderKind = LanguagePackageProviderKind.InMemory,
            InMemoryResources = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [key] = "pending-load",
            },
        };
        var registration = registry.Register(descriptor, "owner.lifecycle.pending");
        if (!registration.Succeeded)
        {
            throw new InvalidOperationException(registration.Error!.Message);
        }

        var lease = localization.ActivateScope(new LocalizationLookupContext(pluginId: scopeId));
        var gate = provider.BlockNext(packageId, ignoreCancellation: true);
        var lookup = localization.GetStringAsync(
            key,
            new LocalizationLookupContext(pluginId: scopeId),
            cancellationToken).AsTask();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        var dispose = host.DisposeAsync().AsTask();
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        var waited = !dispose.IsCompleted;
        gate.Release();
        try
        {
            await lookup.ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }

        await dispose.ConfigureAwait(false);
        lease.Dispose();
        return waited
            && dispose.IsCompletedSuccessfully
            && provider.ActiveLoadCount == 0
            && await ThrowsObjectDisposedAsync(
                () => localization.GetStringAsync("Common.Save", cancellationToken).AsTask())
                .ConfigureAwait(false);
    }

    private static bool ThrowsObjectDisposed(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    private static async Task<bool> ThrowsObjectDisposedAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    private sealed record HostCycleResult(
        bool BusinessFlowPassed,
        bool IdempotentShutdownPassed,
        bool PostDisposePassed,
        IReadOnlyList<WeakReference> WeakReferences);
}
