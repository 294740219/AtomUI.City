using System.Collections.Concurrent;
using System.Globalization;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Localization;
using AtomUI.City.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>通过显式 gate 重复制造 Localization 的关键并发交错。</summary>
public static class PhaseM
{
    private const int ConcurrentWaiters = 64;

    public static async Task RunAsync(
        StressExecutionOptions options,
        CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);

        var services = host.Services;
        var localization = services.GetRequiredService<ILocalizationService>();
        var registry = services.GetRequiredService<LanguagePackageRegistry>();
        var provider = services.GetRequiredService<StressLanguagePackageProvider>();
        var bridge = services.GetRequiredService<StressPresentationLocalizationBridge>();
        var repeat = options.RaceIterations;

        var fifoPasses = 0;
        for (var iteration = 0; iteration < repeat; iteration++)
        {
            var revision = localization.CultureRevision;
            var gate = bridge.BlockNextApply();
            var first = localization.SetCultureAsync("fr-FR", cancellationToken).AsTask();
            await gate.Entered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            var second = localization.SetCultureAsync("de-DE", cancellationToken).AsTask();
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            var secondWaited = !second.IsCompleted;
            gate.Release();
            var results = await Task.WhenAll(first, second).ConfigureAwait(false);
            var states = bridge.AppliedStates;
            if (secondWaited
                && results.All(result => result.Succeeded)
                && localization.CurrentCulture.Name == "de-DE"
                && localization.CultureRevision == revision + 2
                && states[^2].CurrentCulture.Name == "fr-FR"
                && states[^1].CurrentCulture.Name == "de-DE")
            {
                fifoPasses++;
            }
        }

        var coalescedPasses = 0;
        var cancelledWaiters = 0;
        for (var iteration = 0; iteration < repeat; iteration++)
        {
            var packageId = $"Race.Coalesce.{iteration}";
            var scopeId = $"fixtures.plugin.coalesce.{iteration}";
            var key = $"Race.Coalesce.Key.{iteration}";
            var contributionId = $"fixtures.race.coalesce.{iteration}";
            var descriptor = CreateDescriptor(packageId, scopeId, key, "coalesced", contributionId, localization.CurrentCulture);
            EnsureRegistered(registry.Register(descriptor, $"owner.coalesce.{iteration}"));
            using var lease = localization.ActivateScope(new LocalizationLookupContext(pluginId: scopeId));
            var gate = provider.BlockNext(packageId);
            var cancellations = Enumerable.Range(0, ConcurrentWaiters)
                .Select(_ => CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                .ToArray();
            var tasks = cancellations
                .Select(source => LookupAsync(localization, key, scopeId, source.Token))
                .ToArray();

            await gate.Entered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            for (var waiter = 0; waiter < ConcurrentWaiters / 4; waiter++)
            {
                cancellations[waiter].Cancel();
            }

            gate.Release();
            var outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);
            var cancelled = outcomes.Count(outcome => outcome.Cancelled);
            var succeeded = outcomes.Count(outcome => outcome.Value == "coalesced");
            cancelledWaiters += cancelled;
            if (cancelled == ConcurrentWaiters / 4
                && succeeded == ConcurrentWaiters - cancelled
                && provider.GetLoadCount(localization.CurrentCulture.Name, packageId) == 1)
            {
                coalescedPasses++;
            }

            foreach (var source in cancellations)
            {
                source.Dispose();
            }

            await localization.RevokePackagesByContributionIdAsync(contributionId, cancellationToken)
                .ConfigureAwait(false);
        }

        var revokePasses = 0;
        for (var iteration = 0; iteration < repeat; iteration++)
        {
            var packageId = $"Race.Revoke.{iteration}";
            var scopeId = $"fixtures.plugin.revoke.{iteration}";
            var key = $"Race.Revoke.Key.{iteration}";
            var contributionId = $"fixtures.race.revoke.{iteration}";
            var descriptor = CreateDescriptor(packageId, scopeId, key, "snapshot-value", contributionId, localization.CurrentCulture);
            EnsureRegistered(registry.Register(descriptor, $"owner.revoke.{iteration}"));
            using var lease = localization.ActivateScope(new LocalizationLookupContext(pluginId: scopeId));
            var gate = provider.BlockNext(packageId, ignoreCancellation: true);
            var lookup = localization.GetStringAsync(
                key,
                new LocalizationLookupContext(pluginId: scopeId),
                cancellationToken).AsTask();
            await gate.Entered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            var revoke = await localization.RevokePackagesByContributionIdAsync(contributionId, cancellationToken)
                .ConfigureAwait(false);
            gate.Release();
            var snapshotResult = await lookup.ConfigureAwait(false);
            var afterRevoke = await localization.GetStringAsync(
                key,
                new LocalizationLookupContext(pluginId: scopeId),
                cancellationToken).ConfigureAwait(false);
            if (revoke == 1
                && snapshotResult.IsMissing
                && afterRevoke.IsMissing
                && registry.Descriptors.All(candidate => candidate.PackageId != packageId)
                && !localization.CultureState.Value.LoadedPackageIds.Contains(packageId, StringComparer.Ordinal))
            {
                revokePasses++;
            }
        }

        var scopeSnapshotPasses = 0;
        for (var iteration = 0; iteration < repeat; iteration++)
        {
            var packageId = $"Race.Scope.{iteration}";
            var scopeId = $"fixtures.plugin.scope.{iteration}";
            var key = $"Race.Scope.Key.{iteration}";
            var descriptor = CreateDescriptor(packageId, scopeId, key, "scope-snapshot", null, localization.CurrentCulture);
            EnsureRegistered(registry.Register(descriptor, $"owner.scope.{iteration}"));
            var context = new LocalizationLookupContext(pluginId: scopeId);
            var lease = localization.ActivateScope(context);
            var gate = provider.BlockNext(packageId);
            var lookup = localization.GetStringAsync(key, context, cancellationToken).AsTask();
            await gate.Entered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            lease.Dispose();
            gate.Release();
            var snapshotResult = await lookup.ConfigureAwait(false);
            var inactiveResult = await localization.GetStringAsync(key, context, cancellationToken).ConfigureAwait(false);
            if (snapshotResult.Value == "scope-snapshot" && inactiveResult.IsMissing)
            {
                scopeSnapshotPasses++;
            }
        }

        var callbackCounts = new ConcurrentBag<int>();
        var disposeWaitPasses = 0;
        var disposedTexts = new List<ILocalizedText>(repeat);
        for (var iteration = 0; iteration < repeat; iteration++)
        {
            var text = await localization.CreateTextAsync("Common.Save", cancellationToken).ConfigureAwait(false);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbacks = 0;
            text.Changed += (_, _) =>
            {
                Interlocked.Increment(ref callbacks);
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
            };

            var targetCulture = localization.CurrentCulture.Name == "en-US" ? "zh-CN" : "en-US";
            var mutation = localization.SetCultureAsync(targetCulture, cancellationToken).AsTask();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            var disposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var dispose = Task.Run(() =>
            {
                disposeStarted.TrySetResult();
                text.Dispose();
            }, cancellationToken);
            await disposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)
                .ConfigureAwait(false);
            var waited = !dispose.IsCompleted;
            release.TrySetResult();
            await Task.WhenAll(mutation, dispose).ConfigureAwait(false);
            callbackCounts.Add(callbacks);
            if (waited && callbacks == 1)
            {
                disposeWaitPasses++;
            }

            disposedTexts.Add(text);
        }

        await localization.SetCultureAsync(
            localization.CurrentCulture.Name == "en-US" ? "zh-CN" : "en-US",
            cancellationToken).ConfigureAwait(false);
        var noPostDisposeCallbacks = callbackCounts.All(count => count == 1);

        LocalizationResult? reentrantSwitch = null;
        Exception? reentrantRevoke = null;
        bridge.InvokeOnNextApply(async () =>
        {
            reentrantSwitch = await localization.SetCultureAsync("ja-JP", cancellationToken).ConfigureAwait(false);
        });
        var outerSwitch = await localization.SetCultureAsync("fr-FR", cancellationToken).ConfigureAwait(false);
        bridge.InvokeOnNextApply(async () =>
        {
            try
            {
                await localization.RevokePackagesByContributionIdAsync("fixtures.none", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                reentrantRevoke = exception;
            }
        });
        var secondOuterSwitch = await localization.SetCultureAsync("de-DE", cancellationToken).ConfigureAwait(false);
        var reentrancyOk = outerSwitch.Succeeded
            && secondOuterSwitch.Succeeded
            && reentrantSwitch is { Succeeded: false, Error.Kind: LocalizationErrorKind.ReentrantOperation }
            && reentrantRevoke is InvalidOperationException;

        var disposePackageId = "Race.Dispose.Pending";
        var disposeScopeId = "fixtures.plugin.dispose.pending";
        var disposeKey = "Race.Dispose.Pending.Key";
        EnsureRegistered(registry.Register(
            CreateDescriptor(
                disposePackageId,
                disposeScopeId,
                disposeKey,
                "pending",
                null,
                localization.CurrentCulture),
            "owner.dispose.pending"));
        using var disposeLease = localization.ActivateScope(new LocalizationLookupContext(pluginId: disposeScopeId));
        var disposeGate = provider.BlockNext(disposePackageId, ignoreCancellation: true);
        var pendingLookup = localization.GetStringAsync(
            disposeKey,
            new LocalizationLookupContext(pluginId: disposeScopeId),
            cancellationToken).AsTask();
        await disposeGate.Entered.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        var firstDispose = localization.DisposeAsync().AsTask();
        var secondDispose = localization.DisposeAsync().AsTask();
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        var disposeWaited = !firstDispose.IsCompleted && !secondDispose.IsCompleted;
        disposeGate.Release();
        try
        {
            await pendingLookup.ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }

        await Task.WhenAll(firstDispose, secondDispose).ConfigureAwait(false);
        var disposeConverged = disposeWaited
            && firstDispose.IsCompletedSuccessfully
            && secondDispose.IsCompletedSuccessfully
            && provider.ActiveLoadCount == 0
            && bridge.ActiveApplyCount == 0;

        FixtureState.Report.Record(
            "M01-localization-mutation-fifo",
            "并发 culture mutation 在 Bridge 阻塞时严格 FIFO 且 revision 连续",
            fifoPasses == repeat,
            $"passes={fifoPasses}/{repeat} maxBridgeConcurrency={bridge.MaximumConcurrentApplies}");
        FixtureState.Report.Record(
            "M02-localization-load-waiters",
            "64 个共享 load waiter 的取消相互隔离且每个 package 仅加载一次",
            coalescedPasses == repeat,
            $"passes={coalescedPasses}/{repeat} cancelled={cancelledWaiters}");
        FixtureState.Report.Record(
            "M03-localization-load-revoke",
            "load 与 contribution revoke 交错时在途 lookup fallback 且缓存不得复活",
            revokePasses == repeat,
            $"passes={revokePasses}/{repeat}");
        FixtureState.Report.Record(
            "M04-localization-scope-snapshot",
            "lookup 持有开始时 scope snapshot，lease 释放后新 lookup 不再可见",
            scopeSnapshotPasses == repeat,
            $"passes={scopeSnapshotPasses}/{repeat}");
        FixtureState.Report.Record(
            "M05-localization-text-dispose",
            "外部 Dispose 等待在途 callback，返回后不再开始通知",
            disposeWaitPasses == repeat && noPostDisposeCallbacks,
            $"waited={disposeWaitPasses}/{repeat} callbacks={callbackCounts.Sum()}");
        FixtureState.Report.Record(
            "M06-localization-reentrancy-dispose",
            "mutation callback 重入快速失败，并发 service Dispose 等待忽略取消的 load 后合并完成",
            reentrancyOk && disposeConverged,
            $"reentrant={reentrantSwitch?.Error?.Kind} disposeWaited={disposeWaited}");

        await host.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private static LanguagePackageDescriptor CreateDescriptor(
        string packageId,
        string scopeId,
        string key,
        string value,
        string? contributionId,
        CultureInfo culture)
    {
        return new LanguagePackageDescriptor(packageId, culture, ResourceScope.Plugin)
        {
            ScopeId = scopeId,
            ProviderKind = LanguagePackageProviderKind.InMemory,
            ContributionId = contributionId,
            InMemoryResources = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [key] = value,
            },
        };
    }

    private static void EnsureRegistered(LocalizationResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }
    }

    private static async Task<LookupOutcome> LookupAsync(
        ILocalizationService localization,
        string key,
        string scopeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await localization.GetStringAsync(
                key,
                new LocalizationLookupContext(pluginId: scopeId),
                cancellationToken).ConfigureAwait(false);
            return new LookupOutcome(value.Value, Cancelled: false);
        }
        catch (OperationCanceledException)
        {
            return new LookupOutcome(null, Cancelled: true);
        }
    }

    private sealed record LookupOutcome(string? Value, bool Cancelled);
}
