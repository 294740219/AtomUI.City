using System.Collections.Concurrent;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Core.Mvp.Conflict;
using AtomUI.City.Core.Mvp.Data;
using AtomUI.City.Core.Mvp.Diagnostics;
using AtomUI.City.Core.Mvp.Foundation;
using AtomUI.City.Core.Mvp.Operations;
using AtomUI.City.Core.Mvp.Reporting;
using AtomUI.City.Core.Mvp.Unselected;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AtomUI.City.Core.MvpCli;

internal static class MvpVerifier
{
    private const int SelectedServiceCount = 27;
    private const int ExpectedPermutationCount = 120;
    private const int ExpectedCombinationCount = 32;
    private const int ExpectedConcurrentScopeCount = 64;

    private static readonly RootModule[] FullRoots =
    [
        RootModule.Foundation,
        RootModule.Data,
        RootModule.Operations,
        RootModule.Reporting,
        RootModule.Diagnostics,
    ];

    public static async Task<MvpVerificationResult> RunAsync(string[] args)
    {
        if (!TryParseScenario(args, out var scenario))
        {
            return Result("invalid", ["Usage: verify --scenario baseline|all|permutations|combinations|policies|concurrency|isolation|conflict"], exitCode: 2);
        }

        var failures = new List<string>();
        var permutations = 0;
        var combinations = 0;
        var scopes = 0;

        try
        {
            switch (scenario)
            {
                case "baseline":
                    await VerifyBaselineAsync(failures).ConfigureAwait(false);
                    break;
                case "all":
                    await VerifyBaselineAsync(failures).ConfigureAwait(false);
                    await VerifyAllServicesAsync(failures).ConfigureAwait(false);
                    permutations = await VerifyPermutationsAsync(failures).ConfigureAwait(false);
                    combinations = await VerifyCombinationsAsync(failures).ConfigureAwait(false);
                    await VerifyPoliciesAsync(failures).ConfigureAwait(false);
                    scopes = await VerifyConcurrencyAsync(failures).ConfigureAwait(false);
                    await VerifyIsolationAsync(failures).ConfigureAwait(false);
                    await VerifyConflictAsync(failures).ConfigureAwait(false);
                    break;
                case "permutations":
                    permutations = await VerifyPermutationsAsync(failures).ConfigureAwait(false);
                    break;
                case "combinations":
                    combinations = await VerifyCombinationsAsync(failures).ConfigureAwait(false);
                    break;
                case "policies":
                    await VerifyPoliciesAsync(failures).ConfigureAwait(false);
                    break;
                case "concurrency":
                    scopes = await VerifyConcurrencyAsync(failures).ConfigureAwait(false);
                    break;
                case "isolation":
                    await VerifyIsolationAsync(failures).ConfigureAwait(false);
                    break;
                case "conflict":
                    await VerifyConflictAsync(failures).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception exception)
        {
            failures.Add($"Unexpected {exception.GetType().FullName}: {exception.Message}");
        }

        return new MvpVerificationResult(
            scenario,
            failures.Count == 0,
            failures.Count == 0 ? 0 : 1,
            6,
            SelectedServiceCount,
            permutations,
            combinations,
            scopes,
            failures.ToArray());
    }

    private static async Task VerifyBaselineAsync(List<string> failures)
    {
        var built = BuildHost([]);
        await using var host = built.Host;
        CheckModuleSet(host, [typeof(MvpApplicationModule), typeof(FoundationModule)], failures, "baseline");
        Check(host.Services.GetRequiredService<IMvpClock>() is MvpClock, "baseline clock was not generated", failures);
        Check(host.Services.GetRequiredService<MvpApplicationInfo>().Name == "AtomUI.City.Core.MvpCli", "baseline app service is invalid", failures);
        Check(host.Services.GetService<DataCache>() is null, "baseline unexpectedly contains Data services", failures);
        Check(host.Services.GetService<UnselectedSingleton>() is null, "baseline contains unselected services", failures);
        await StartStopAsync(host).ConfigureAwait(false);
    }

    private static async Task VerifyAllServicesAsync(List<string> failures)
    {
        DataUnitOfWork.Reset();
        SelectedHostedService.Reset();
        var built = BuildHost(FullRoots);
        await using var host = built.Host;
        CheckModuleSet(host, ExpectedFullModules(), failures, "all");
        VerifyDescriptorImplementations(built.Descriptors, failures);

        var clock = host.Services.GetRequiredService<IMvpClock>();
        Check(ReferenceEquals(clock, host.Services.GetRequiredService<IMvpClock>()), "singleton clock identity changed", failures);
        Check(host.Services.GetRequiredKeyedService<IFoundationStrategy>("primary").Name == "primary", "primary key mismatch", failures);
        Check(host.Services.GetRequiredKeyedService<IFoundationStrategy>("secondary").Name == "secondary", "secondary key mismatch", failures);
        Check(host.Services.GetRequiredKeyedService<IReportExporter>("json").Format == "json", "json exporter key mismatch", failures);
        Check(host.Services.GetRequiredKeyedService<IReportExporter>("text").Format == "text", "text exporter key mismatch", failures);
        Check(host.Services.GetRequiredService<IDataPolicy>().Name == "preconfigured", "TryAdd did not preserve the preconfigured service", failures);
        Check(host.Services.GetRequiredService<IDiagnosticPolicy>().Name == "generated-replace", "Replace did not replace the preconfigured service", failures);
        Check(ReferenceEquals(
            host.Services.GetRequiredService<SelectedHostedService>(),
            host.Services.GetServices<IHostedService>().OfType<SelectedHostedService>().Single()), "hosted multi-contract instance is not shared", failures);

        using (var first = host.Services.CreateScope())
        using (var second = host.Services.CreateScope())
        {
            var firstStoreReader = first.ServiceProvider.GetRequiredService<IRecordReader>();
            var firstStoreWriter = first.ServiceProvider.GetRequiredService<IRecordWriter>();
            Check(ReferenceEquals(firstStoreReader, firstStoreWriter), "scoped multi-contract instance is not shared", failures);
            Check(!ReferenceEquals(firstStoreReader, second.ServiceProvider.GetRequiredService<IRecordReader>()), "scoped store leaked across scopes", failures);
            Check(ReferenceEquals(
                first.ServiceProvider.GetRequiredService<FoundationScope>(),
                first.ServiceProvider.GetRequiredService<FoundationScope>()), "scoped marker changed inside one scope", failures);
            Check(!ReferenceEquals(
                first.ServiceProvider.GetRequiredService<FoundationScope>(),
                second.ServiceProvider.GetRequiredService<FoundationScope>()), "scoped marker leaked across scopes", failures);
            Check(!ReferenceEquals(
                first.ServiceProvider.GetRequiredService<FoundationNonce>(),
                first.ServiceProvider.GetRequiredService<FoundationNonce>()), "transient marker was reused", failures);
            _ = first.ServiceProvider.GetRequiredService<DataUnitOfWork>();
            _ = first.ServiceProvider.GetRequiredService<OrderProcessor>().Execute();
            _ = first.ServiceProvider.GetRequiredService<ReportReader>().Read();
            _ = first.ServiceProvider.GetRequiredService<MvpApplicationSession>();
        }

        Check(DataUnitOfWork.DisposeCount == 1, "disposable scoped service was not disposed exactly once", failures);
        await StartStopAsync(host).ConfigureAwait(false);
        Check(SelectedHostedService.StartCount == 1 && SelectedHostedService.StopCount == 1, "selected hosted service lifecycle count is invalid", failures);
    }

    private static async Task<int> VerifyPermutationsAsync(List<string> failures)
    {
        string[]? baselineSignature = null;
        string[]? baselineModules = null;
        var count = 0;

        foreach (var permutation in Permute(FullRoots))
        {
            var built = BuildHost(permutation);
            await using var host = built.Host;
            var signature = NormalizeDescriptors(built.Descriptors);
            var modules = ModuleNames(host);
            baselineSignature ??= signature;
            baselineModules ??= modules;
            Check(signature.SequenceEqual(baselineSignature), $"descriptor order changed for permutation {count}", failures);
            Check(modules.SequenceEqual(baselineModules), $"module order changed for permutation {count}", failures);
            CheckModuleSet(host, ExpectedFullModules(), failures, $"permutation-{count}");
            using (var scope = host.Services.CreateScope())
            {
                _ = scope.ServiceProvider.GetRequiredService<OrderProcessor>().Execute();
                _ = scope.ServiceProvider.GetRequiredService<ReportReader>().Read();
            }
            await StartStopAsync(host).ConfigureAwait(false);
            count++;
        }

        Check(count == ExpectedPermutationCount, $"expected {ExpectedPermutationCount} permutations but ran {count}", failures);
        return count;
    }

    private static async Task<int> VerifyCombinationsAsync(List<string> failures)
    {
        var count = 0;
        for (var mask = 0; mask < ExpectedCombinationCount; mask++)
        {
            var roots = FullRoots.Where((_, index) => (mask & (1 << index)) != 0).ToArray();
            var built = BuildHost(roots);
            await using var host = built.Host;
            var expected = ExpectedModules(mask);
            CheckModuleSet(host, expected, failures, $"combination-{mask}");

            using (var scope = host.Services.CreateScope())
            {
                Check((scope.ServiceProvider.GetService<DataCache>() is not null) == expected.Contains(typeof(DataModule)), $"Data activation mismatch for combination {mask}", failures);
                Check((scope.ServiceProvider.GetService<OrderProcessor>() is not null) == expected.Contains(typeof(OperationsModule)), $"Operations activation mismatch for combination {mask}", failures);
                Check((scope.ServiceProvider.GetService<ReportReader>() is not null) == expected.Contains(typeof(ReportingModule)), $"Reporting activation mismatch for combination {mask}", failures);
                Check((scope.ServiceProvider.GetService<AuditSink>() is not null) == expected.Contains(typeof(DiagnosticsModule)), $"Diagnostics activation mismatch for combination {mask}", failures);
            }

            await StartStopAsync(host).ConfigureAwait(false);
            count++;
        }

        Check(count == ExpectedCombinationCount, $"expected {ExpectedCombinationCount} combinations but ran {count}", failures);
        return count;
    }

    private static async Task VerifyPoliciesAsync(List<string> failures)
    {
        var generated = BuildHost([RootModule.Data, RootModule.Diagnostics]);
        await using (var host = generated.Host)
        {
            Check(host.Services.GetRequiredService<IDataPolicy>().Name == "preconfigured", "TryAdd policy failed", failures);
            Check(host.Services.GetRequiredService<IDiagnosticPolicy>().Name == "generated-replace", "Replace policy failed", failures);
            await StartStopAsync(host).ConfigureAwait(false);
        }

        var overridden = BuildHost(
            [RootModule.Data, RootModule.Diagnostics],
            services => services.AddSingleton<IDiagnosticPolicy, UserDiagnosticPolicy>());
        await using (var host = overridden.Host)
        {
            Check(host.Services.GetRequiredService<IDiagnosticPolicy>().Name == "user", "user registration did not run last", failures);
            await StartStopAsync(host).ConfigureAwait(false);
        }
    }

    private static async Task<int> VerifyConcurrencyAsync(List<string> failures)
    {
        DataUnitOfWork.Reset();
        var built = BuildHost(FullRoots);
        await using var host = built.Host;
        var singleton = host.Services.GetRequiredService<DataCache>();
        var scopeIds = new ConcurrentBag<Guid>();
        var tasks = Enumerable.Range(0, ExpectedConcurrentScopeCount).Select(index => Task.Run(() =>
        {
            using var scope = host.Services.CreateScope();
            var provider = scope.ServiceProvider;
            if (!ReferenceEquals(singleton, provider.GetRequiredService<DataCache>()))
            {
                throw new InvalidOperationException("Singleton identity changed in a concurrent scope.");
            }

            var scoped = provider.GetRequiredService<FoundationScope>();
            if (!ReferenceEquals(scoped, provider.GetRequiredService<FoundationScope>()))
            {
                throw new InvalidOperationException("Scoped identity changed inside a concurrent scope.");
            }

            if (ReferenceEquals(provider.GetRequiredService<FoundationNonce>(), provider.GetRequiredService<FoundationNonce>()))
            {
                throw new InvalidOperationException("Transient identity was reused in a concurrent scope.");
            }

            _ = provider.GetRequiredService<DataUnitOfWork>();
            _ = provider.GetRequiredKeyedService<IFoundationStrategy>("primary");
            _ = provider.GetRequiredKeyedService<IReportExporter>("json");
            scopeIds.Add(scoped.Id);
        })).ToArray();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add($"Concurrent resolution failed: {exception.Message}");
        }

        Check(scopeIds.Distinct().Count() == ExpectedConcurrentScopeCount, "concurrent scopes shared scoped instances", failures);
        Check(DataUnitOfWork.DisposeCount == ExpectedConcurrentScopeCount, "concurrent scoped disposals were not exact", failures);
        await StartStopAsync(host).ConfigureAwait(false);
        return ExpectedConcurrentScopeCount;
    }

    private static async Task VerifyIsolationAsync(List<string> failures)
    {
        UnselectedHostedService.Reset();
        UnselectedSingleton.Reset();
        var baseline = BuildHost(FullRoots);
        await using (var host = baseline.Host)
        {
            Check(host.Services.GetService<UnselectedSingleton>() is null, "unselected singleton was resolvable", failures);
            Check(host.Services.GetServices<IHostedService>().All(service => service is not UnselectedHostedService), "unselected hosted service entered IEnumerable<IHostedService>", failures);
            await StartStopAsync(host).ConfigureAwait(false);
        }
        Check(UnselectedHostedService.StartCount == 0 && UnselectedSingleton.CreatedCount == 0, "unselected module produced runtime side effects", failures);

        var selected = BuildHost([RootModule.Unselected]);
        await using (var host = selected.Host)
        {
            var failed = false;
            try
            {
                await host.StartAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("Unselected hosted service", StringComparison.Ordinal))
            {
                failed = true;
            }
            catch (Exception exception) when (exception.ToString().Contains("Unselected hosted service", StringComparison.Ordinal))
            {
                failed = true;
            }

            Check(failed, "explicitly selected unselected module did not enter its failing hosted service", failures);
            Check(host.Services.GetRequiredService<IHostDiagnostics>().Records.Any(record => record.Code == HostDiagnosticIds.HostStartFailed), "selected failure did not emit HostStartFailed", failures);
        }
        Check(UnselectedHostedService.StartCount == 1, "selected hosted service did not start exactly once", failures);
    }

    private static Task VerifyConflictAsync(List<string> failures)
    {
        var builder = CreateBuilder();
        builder.UseModule<ConflictModule>();
        var failed = false;
        try
        {
            using var host = builder.Build();
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains(typeof(IMvpClock).FullName!, StringComparison.Ordinal))
        {
            failed = true;
        }
        catch (Exception exception)
        {
            failures.Add($"Conflict produced unexpected {exception.GetType().Name}: {exception.Message}");
        }

        Check(failed, "selected conflict module did not fail deterministically", failures);
        Check(builder.GetBuildDiagnostics().Records.Any(record => record.Code == HostDiagnosticIds.HostBuildFailed), "conflict did not emit HostBuildFailed", failures);
        return Task.CompletedTask;
    }

    private static BuiltHost BuildHost(
        IReadOnlyList<RootModule> roots,
        Action<IServiceCollection>? userServices = null)
    {
        var builder = CreateBuilder();
        foreach (var root in roots)
        {
            AddRoot(builder, root);
        }

        ServiceDescriptor[] descriptors = [];
        builder.ConfigureServices(services =>
        {
            userServices?.Invoke(services);
            descriptors = services.ToArray();
        });
        return new BuiltHost(builder.Build(), descriptors);
    }

    private static IApplicationHostBuilder CreateBuilder()
    {
        var builder = ApplicationHost.CreateBuilder([]);
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.Core.MvpCli";
            options.ApplicationName = "AtomUI.City.Core.MvpCli";
            options.ShutdownTimeout = TimeSpan.FromSeconds(10);
        });
        builder.ConfigureServices(services => services.AddLogging(logging => logging.ClearProviders()));
        return builder;
    }

    private static void AddRoot(IApplicationHostBuilder builder, RootModule root)
    {
        switch (root)
        {
            case RootModule.Foundation:
                builder.UseModule<FoundationModule>();
                break;
            case RootModule.Data:
                builder.UseModule<DataModule>();
                break;
            case RootModule.Operations:
                builder.UseModule<OperationsModule>();
                break;
            case RootModule.Reporting:
                builder.UseModule<ReportingModule>();
                break;
            case RootModule.Diagnostics:
                builder.UseModule<DiagnosticsModule>();
                break;
            case RootModule.Unselected:
                builder.UseModule<UnselectedModule>();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(root), root, "Unknown root module.");
        }
    }

    private static async Task StartStopAsync(IApplicationHost host)
    {
        await host.StartAsync().ConfigureAwait(false);
        await host.StopAsync().ConfigureAwait(false);
    }

    private static void VerifyDescriptorImplementations(IReadOnlyList<ServiceDescriptor> descriptors, List<string> failures)
    {
        var expected = new[]
        {
            typeof(MvpClock), typeof(FoundationScope), typeof(FoundationNonce), typeof(PrimaryFoundationStrategy), typeof(SecondaryFoundationStrategy),
            typeof(RecordStore), typeof(DataCache), typeof(DataUnitOfWork), typeof(DataSerializer),
            typeof(OrderProcessor), typeof(CommandDispatcher), typeof(JobCoordinator), typeof(SelectedHostedService), typeof(OperationProbe),
            typeof(ReportReader), typeof(ReportFormatter), typeof(JsonReportExporter), typeof(TextReportExporter),
            typeof(AuditSink), typeof(MetricsScope), typeof(TraceFormatter), typeof(GeneratedDiagnosticPolicy),
            typeof(MvpApplicationInfo), typeof(MvpApplicationSession), typeof(MvpApplicationCommand), typeof(StartupObserver),
        };

        foreach (var implementation in expected)
        {
            Check(descriptors.Any(descriptor => DescriptorReferences(descriptor, implementation)), $"descriptor for {implementation.FullName} is missing", failures);
        }
        Check(expected.Length == SelectedServiceCount - 1, "selected service declaration accounting changed", failures);
    }

    private static bool DescriptorReferences(ServiceDescriptor descriptor, Type implementation)
    {
        if (descriptor.ServiceType == implementation)
        {
            return true;
        }
        return descriptor.IsKeyedService
            ? descriptor.KeyedImplementationType == implementation
            : descriptor.ImplementationType == implementation;
    }

    private static string[] NormalizeDescriptors(IReadOnlyList<ServiceDescriptor> descriptors) => descriptors
        .Select(descriptor => $"{descriptor.ServiceType.FullName}|{descriptor.Lifetime}|{NormalizeKey(descriptor)}")
        .ToArray();

    private static string NormalizeKey(ServiceDescriptor descriptor) => !descriptor.IsKeyedService
        ? "<none>"
        : descriptor.ServiceKey is string key ? key : "<generated-backing>";

    private static string[] ModuleNames(IApplicationHost host) => host.Services
        .GetRequiredService<IModuleRegistry>()
        .Modules
        .Select(module => module.ModuleType.FullName!)
        .ToArray();

    private static void CheckModuleSet(IApplicationHost host, IReadOnlyCollection<Type> expected, List<string> failures, string scenario)
    {
        var actual = host.Services.GetRequiredService<IModuleRegistry>().Modules.Select(module => module.ModuleType).ToHashSet();
        Check(actual.SetEquals(expected), $"{scenario} module set mismatch: {string.Join(",", actual.Select(type => type.Name))}", failures);
    }

    private static Type[] ExpectedFullModules() =>
    [
        typeof(MvpApplicationModule), typeof(FoundationModule), typeof(DataModule),
        typeof(OperationsModule), typeof(ReportingModule), typeof(DiagnosticsModule),
    ];

    private static HashSet<Type> ExpectedModules(int mask)
    {
        var result = new HashSet<Type> { typeof(MvpApplicationModule), typeof(FoundationModule) };
        var data = (mask & (1 << 1)) != 0;
        var operations = (mask & (1 << 2)) != 0;
        var reporting = (mask & (1 << 3)) != 0;
        var diagnostics = (mask & (1 << 4)) != 0;
        if (data || operations || reporting)
            result.Add(typeof(DataModule));
        if (operations)
            result.Add(typeof(OperationsModule));
        if (reporting)
            result.Add(typeof(ReportingModule));
        if (diagnostics)
            result.Add(typeof(DiagnosticsModule));
        return result;
    }

    private static IEnumerable<RootModule[]> Permute(IReadOnlyList<RootModule> values)
    {
        var buffer = values.ToArray();
        return PermuteCore(0);

        IEnumerable<RootModule[]> PermuteCore(int index)
        {
            if (index == buffer.Length)
            {
                yield return buffer.ToArray();
                yield break;
            }
            for (var current = index; current < buffer.Length; current++)
            {
                (buffer[index], buffer[current]) = (buffer[current], buffer[index]);
                foreach (var permutation in PermuteCore(index + 1))
                    yield return permutation;
                (buffer[index], buffer[current]) = (buffer[current], buffer[index]);
            }
        }
    }

    private static bool TryParseScenario(string[] args, out string scenario)
    {
        scenario = string.Empty;
        if (args.Length != 3 || args[0] != "verify" || args[1] != "--scenario")
            return false;
        scenario = args[2];
        return scenario is "baseline" or "all" or "permutations" or "combinations" or "policies" or "concurrency" or "isolation" or "conflict";
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        if (!condition)
            failures.Add(message);
    }

    private static MvpVerificationResult Result(string scenario, string[] failures, int exitCode) =>
        new(scenario, false, exitCode, 0, 0, 0, 0, 0, failures);

    private sealed record BuiltHost(IApplicationHost Host, ServiceDescriptor[] Descriptors);

    private enum RootModule
    {
        Foundation,
        Data,
        Operations,
        Reporting,
        Diagnostics,
        Unselected,
    }
}
