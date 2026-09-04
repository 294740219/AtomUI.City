using System.Text.Json;
using AtomUI.City.Testing.Processes;

namespace AtomUI.City.Core.Tests;

[Collection(ProcessTestCollection.Name)]
public sealed class CoreHeadlessProcessTests
{
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    [Theory]
    [InlineData("build", "Core fixture build failure.")]
    [InlineData("di", "Unable to resolve service")]
    [InlineData("start", "Core fixture start failure.")]
    public async Task HeadlessEntryFailureIsReportedWithoutUnhandledExceptionEscape(
        string phase,
        string expectedMessage)
    {
        var result = await RunApplicationAsync("--test-entry-failure", phase);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(expectedMessage, result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeadlessHungEntryIsTerminatedByTheProcessTimeout()
    {
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            RunApplicationAsync(TimeSpan.FromMilliseconds(500), "--test-entry-hang"));

        Assert.Contains("did not exit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeadlessLifecycleRunsToCompletionWithoutUiRuntime()
    {
        var result = await RunScenarioAsync("lifecycle");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(1, result.GetProperty("hostedStartCount").GetInt32());
        Assert.Equal(1, result.GetProperty("hostedStopCount").GetInt32());
        Assert.True(result.GetProperty("scopedDisposed").GetBoolean());
        Assert.Equal("Disposed", result.GetProperty("hostScopeState").GetString());
        Assert.Equal("Disposed", result.GetProperty("applicationScopeState").GetString());
        Assert.Contains("AUCHOST002", ReadStrings(result, "diagnostics"));
        Assert.Contains("AUCHOST003", ReadStrings(result, "diagnostics"));
    }

    [Fact]
    public async Task HeadlessStartupFailureReturnsOriginalErrorAfterRollback()
    {
        var result = await RunScenarioAsync("startup-failure");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(typeof(InvalidOperationException).FullName, result.GetProperty("errorType").GetString());
        Assert.Equal(
            ["foundation:init", "failing:init", "failing:shutdown", "foundation:shutdown"],
            ReadStrings(result, "calls"));
        Assert.Contains("AUCHOST102", ReadStrings(result, "diagnostics"));
    }

    [Fact]
    public async Task HeadlessStartupFailureReturnsAllRollbackFailuresAfterCleanup()
    {
        var result = await RunScenarioAsync("startup-rollback-failure");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(
            [
                "headless startup initialization failed",
                "headless application rollback failed",
                "headless foundation rollback failed",
            ],
            ReadStrings(result, "failureMessages"));
        Assert.Equal(2, result.GetProperty("rollbackDiagnosticCount").GetInt32());
        Assert.Equal("Disposed", result.GetProperty("hostScopeState").GetString());
    }

    [Fact]
    public async Task HeadlessShutdownAggregatesFailuresAndStillStopsHostedServices()
    {
        var result = await RunScenarioAsync("shutdown-failure");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(2, result.GetProperty("failureCount").GetInt32());
        Assert.Equal(1, result.GetProperty("hostedStopCount").GetInt32());
        Assert.Equal(["second:shutdown", "first:shutdown"], ReadStrings(result, "calls"));
        Assert.Contains("AUCHOST103", ReadStrings(result, "diagnostics"));
    }

    [Fact]
    public async Task HeadlessLifecycleFailureEmitsCorrelatedStructuredDiagnostics()
    {
        var result = await RunScenarioAsync("lifecycle-diagnostics");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal("Application.Stop", result.GetProperty("stage").GetString());
        Assert.EndsWith(
            "HeadlessStopFailureMiddleware",
            result.GetProperty("middlewareType").GetString(),
            StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{32}$", result.GetProperty("operationId").GetString()!);
        Assert.Equal(
            result.GetProperty("operationId").GetString(),
            result.GetProperty("summaryOperationId").GetString());
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            result.GetProperty("exceptionType").GetString());
        Assert.Equal(1, result.GetProperty("hostedStopCount").GetInt32());
        Assert.Equal("Stopped", result.GetProperty("hostScopeStateAfterStop").GetString());
    }

    [Fact]
    public async Task HeadlessRunCancellationStillExecutesShutdown()
    {
        var result = await RunScenarioAsync("run-cancellation");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("canceled").GetBoolean());
        Assert.Equal(1, result.GetProperty("hostedStopCount").GetInt32());
        Assert.Contains("cancellation:shutdown", ReadStrings(result, "calls"));
    }

    [Fact]
    public async Task HeadlessConcurrentStopSharesOneTransaction()
    {
        var result = await RunScenarioAsync("concurrent-stop");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("sharedTransaction").GetBoolean());
        Assert.Equal(1, result.GetProperty("shutdownCount").GetInt32());
        Assert.Equal("Disposed", result.GetProperty("hostScopeState").GetString());
    }

    [Fact]
    public async Task HeadlessStopBeforeStartRunsCompleteBuildResourceCleanup()
    {
        var result = await RunScenarioAsync("stop-before-start");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("sharedTransaction").GetBoolean());
        Assert.True(result.GetProperty("hostScopeCanceled").GetBoolean());
        Assert.Equal("Stopped", result.GetProperty("hostScopeStateAfterStop").GetString());
        Assert.Equal("Disposed", result.GetProperty("hostScopeStateAfterDispose").GetString());
        Assert.Equal("Stopped", result.GetProperty("childScopeStateAfterStop").GetString());
        Assert.Equal(1, result.GetProperty("moduleDisposeCount").GetInt32());
        Assert.Equal(0, result.GetProperty("moduleShutdownCount").GetInt32());
        Assert.Equal(0, result.GetProperty("hostedStartCount").GetInt32());
        Assert.Equal(0, result.GetProperty("hostedStopCount").GetInt32());
        Assert.Equal(1, result.GetProperty("stopMiddlewareCount").GetInt32());
        Assert.Equal(1, result.GetProperty("hostStoppedDiagnosticCount").GetInt32());
        Assert.Matches("^[0-9a-f]{32}$", result.GetProperty("operationId").GetString()!);
        Assert.True(result.GetProperty("startRejected").GetBoolean());
    }

    [Fact]
    public async Task HeadlessConfigurationFreezeGuardsEveryEscapedWriteHandle()
    {
        var result = await RunScenarioAsync("configuration-freeze");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("configurationCollectionsWritableBeforeBuild").GetBoolean());
        Assert.True(result.GetProperty("configurationCollectionsReadOnlyAfterBuild").GetBoolean());
        Assert.True(result.GetProperty("sectionIndexerRejected").GetBoolean());
        Assert.True(result.GetProperty("sectionValueRejected").GetBoolean());
        Assert.True(result.GetProperty("nestedSectionRejected").GetBoolean());
        Assert.True(result.GetProperty("managerChildRejected").GetBoolean());
        Assert.True(result.GetProperty("lazyChildRejected").GetBoolean());
        Assert.True(result.GetProperty("rootIndexerRejected").GetBoolean());
        Assert.True(result.GetProperty("rootSectionRejected").GetBoolean());
        Assert.True(result.GetProperty("rootChildRejected").GetBoolean());
        Assert.True(result.GetProperty("rootReloadRejected").GetBoolean());
        Assert.True(result.GetProperty("providerSetRejected").GetBoolean());
        Assert.True(result.GetProperty("providerLoadRejected").GetBoolean());
        Assert.Equal(64, result.GetProperty("capturedSectionCount").GetInt32());
        Assert.Equal(64, result.GetProperty("rejectedSectionWriteCount").GetInt32());
        Assert.True(result.GetProperty("originalValuesPreserved").GetBoolean());
    }

    [Fact]
    public async Task HeadlessLifecycleScopeStopJoinsConcurrentChildDisposeTransactions()
    {
        var result = await RunScenarioAsync("scope-dispose-race");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(64, result.GetProperty("childCount").GetInt32());
        Assert.Equal(32, result.GetProperty("synchronousDisposeCount").GetInt32());
        Assert.Equal(32, result.GetProperty("asynchronousDisposeCount").GetInt32());
        Assert.Equal(64, result.GetProperty("disposedChildCount").GetInt32());
        Assert.Equal(0, result.GetProperty("remainingChildCount").GetInt32());
        Assert.Equal(0, result.GetProperty("cleanupFailureCount").GetInt32());
        Assert.Equal("Stopped", result.GetProperty("rootStateAfterStop").GetString());
        Assert.Equal("Disposed", result.GetProperty("rootStateAfterDispose").GetString());
        Assert.True(result.GetProperty("publicStopRejected").GetBoolean());
    }

    [Fact]
    public async Task HeadlessModuleRegistryUsesOneTerminalTransaction()
    {
        var result = await RunScenarioAsync("module-registry-race");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(5, result.GetProperty("moduleCount").GetInt32());
        Assert.Equal(64, result.GetProperty("concurrentCallerCount").GetInt32());
        Assert.Equal(5, result.GetProperty("shutdownFirstShutdownOrder").GetArrayLength());
        Assert.Equal(5, result.GetProperty("shutdownFirstDisposeOrder").GetArrayLength());
        Assert.False(result.GetProperty("shutdownFirstOverlapDetected").GetBoolean());
        Assert.Equal(5, result.GetProperty("disposeFirstShutdownCount").GetInt32());
        Assert.Equal(5, result.GetProperty("disposeFirstDisposeOrder").GetArrayLength());
        Assert.False(result.GetProperty("disposeFirstOverlapDetected").GetBoolean());
    }

    [Fact]
    public async Task HeadlessModuleRegistryExposesMetadataWithoutLifecycleControl()
    {
        var result = await RunScenarioAsync("module-registry-ownership");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(["get_Modules"], result.GetProperty("publicMethods")
            .EnumerateArray().Select(item => item.GetString()));
        Assert.True(result.GetProperty("disposalCapabilityHidden").GetBoolean());
        Assert.True(result.GetProperty("implementationHidden").GetBoolean());
        Assert.True(result.GetProperty("moduleMetadataAvailable").GetBoolean());
    }

    [Fact]
    public async Task HeadlessModuleGraphCycleFailsBeforeConstructorsRun()
    {
        var result = await RunScenarioAsync("module-graph-cycle");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(0, result.GetProperty("firstConstructorCount").GetInt32());
        Assert.Equal(0, result.GetProperty("secondConstructorCount").GetInt32());
        Assert.True(result.GetProperty("completeCyclePathReported").GetBoolean());
    }

    [Fact]
    public async Task HeadlessDiagnosticsEnforcesInputAndHostCompletionContracts()
    {
        var result = await RunScenarioAsync("diagnostics-contract");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(3, result.GetProperty("invalidValueRejectionCount").GetInt32());
        Assert.Equal(512, result.GetProperty("writerCount").GetInt32());
        Assert.Equal(
            512,
            result.GetProperty("acceptedWriteCount").GetInt32() +
            result.GetProperty("rejectedWriteCount").GetInt32());
        Assert.Equal(256, result.GetProperty("acceptedWriteCount").GetInt32());
        Assert.Equal(256, result.GetProperty("rejectedWriteCount").GetInt32());
        Assert.True(result.GetProperty("concurrentBoundaryPreserved").GetBoolean());
        Assert.True(result.GetProperty("hostStoppedRecordRetained").GetBoolean());
        Assert.True(result.GetProperty("writeAfterHostDisposeRejected").GetBoolean());
        Assert.True(result.GetProperty("recordsReadableAfterHostDispose").GetBoolean());
    }

    [Fact]
    public async Task HeadlessPublicBoundariesRejectInvalidModelsAndProtectGlobalTables()
    {
        var result = await RunScenarioAsync("public-boundaries");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("unknownStageAreaRejected").GetBoolean());
        Assert.True(result.GetProperty("unknownRootScopeKindRejected").GetBoolean());
        Assert.True(result.GetProperty("unknownChildScopeKindRejected").GetBoolean());
        Assert.True(result.GetProperty("invalidChildWasNotAttached").GetBoolean());
        Assert.True(result.GetProperty("unknownServiceLifetimeRejected").GetBoolean());
        Assert.True(result.GetProperty("nullScopedServiceTypeRejected").GetBoolean());
        Assert.True(result.GetProperty("nullExposedServiceTypeRejected").GetBoolean());
        Assert.True(result.GetProperty("emptyServiceTypeListsRemainValid").GetBoolean());
        Assert.True(result.GetProperty("nullModuleDependencyRejected").GetBoolean());
        Assert.True(result.GetProperty("defaultLifecycleContextStageRejected").GetBoolean());
        Assert.True(result.GetProperty("defaultLifecycleMiddlewareStageRejected").GetBoolean());
        Assert.True(result.GetProperty("defaultDiagnosticStageRejected").GetBoolean());
        Assert.True(result.GetProperty("globalStageTableImmutable").GetBoolean());
    }

    [Fact]
    public async Task HeadlessBuildKeepsServiceConfigurationSynchronousAndInitializationAsync()
    {
        var result = await RunScenarioAsync("sync-service-configuration");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(1, result.GetProperty("configurationCount").GetInt32());
        Assert.Equal(0, result.GetProperty("initializationCountBeforeStart").GetInt32());
        Assert.Equal(1, result.GetProperty("initializationCountAfterStart").GetInt32());
        Assert.Equal(0, result.GetProperty("synchronizationContextPostCount").GetInt32());
    }

    [Fact]
    public async Task HeadlessBuildFailurePreservesCleanupFailuresAndContinuesRollback()
    {
        var result = await RunScenarioAsync("build-cleanup-failure");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(3, result.GetProperty("failureMessages").GetArrayLength());
        Assert.Equal(1, result.GetProperty("failingDisposeCount").GetInt32());
        Assert.Equal(1, result.GetProperty("successfulDisposeCount").GetInt32());
        Assert.Equal(1, result.GetProperty("foundationDisposeCount").GetInt32());
        Assert.Equal(2, result.GetProperty("cleanupDiagnosticCount").GetInt32());
        Assert.Equal(2, result.GetProperty("moduleDisposeDiagnosticCount").GetInt32());
    }

    [Fact]
    public async Task HeadlessBuildFailureAwaitsAsyncCleanupWithoutUiContextDeadlock()
    {
        var result = await RunScenarioAsync("build-async-cleanup-context");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("configurationBuildReturned").GetBoolean());
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            result.GetProperty("configurationErrorType").GetString());
        Assert.Equal(0, result.GetProperty("configurationSynchronizationContextPostCount").GetInt32());
        Assert.Equal(1, result.GetProperty("configurationDisposeCount").GetInt32());
        Assert.True(result.GetProperty("constructionBuildReturned").GetBoolean());
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            result.GetProperty("constructionErrorType").GetString());
        Assert.Equal(0, result.GetProperty("constructionSynchronizationContextPostCount").GetInt32());
        Assert.Equal(1, result.GetProperty("constructionDisposeCount").GetInt32());
    }

    [Fact]
    public async Task HeadlessBuildFailureAwaitsAsyncOnlyGenericHostCleanupWithoutUiContextDeadlock()
    {
        var result = await RunScenarioAsync("build-generic-host-async-cleanup");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("buildReturned").GetBoolean());
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            result.GetProperty("errorType").GetString());
        Assert.Equal("headless generic host build failed", result.GetProperty("error").GetString());
        Assert.Equal(0, result.GetProperty("synchronizationContextPostCount").GetInt32());
        Assert.Equal(1, result.GetProperty("disposeCount").GetInt32());
        Assert.True(result.GetProperty("disposeCompleted").GetBoolean());
        Assert.Equal(0, result.GetProperty("cleanupDiagnosticCount").GetInt32());
    }

    [Fact]
    public async Task HeadlessBuildAsyncCleanupTimeoutIsBoundedAndObservable()
    {
        var result = await RunScenarioAsync("build-async-cleanup-timeout");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("buildReturned").GetBoolean());
        Assert.Equal(typeof(AggregateException).FullName, result.GetProperty("errorType").GetString());
        Assert.Equal(0, result.GetProperty("synchronizationContextPostCount").GetInt32());
        Assert.Equal(1, result.GetProperty("disposeCount").GetInt32());
        Assert.True(result.GetProperty("incompleteAtTimeout").GetBoolean());
        Assert.True(result.GetProperty("completedAfterRelease").GetBoolean());
        Assert.Equal(TimeSpan.FromMilliseconds(100).ToString(), result.GetProperty("cleanupTimeout").GetString());
        Assert.Equal(bool.TrueString, result.GetProperty("cleanupMayStillBeRunning").GetString());
    }

    [Fact]
    public async Task HeadlessLifecycleNextCannotEscapeItsTransaction()
    {
        var result = await RunScenarioAsync("lifecycle-next-ownership");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("startStayedPending").GetBoolean());
        Assert.True(result.GetProperty("fireAndForgetRejected").GetBoolean());
        Assert.Equal(1, result.GetProperty("hostedStartCount").GetInt32());
        Assert.Equal(1, result.GetProperty("hostedStopCount").GetInt32());
        Assert.True(result.GetProperty("stopStayedPending").GetBoolean());
        Assert.True(result.GetProperty("stopFireAndForgetRejected").GetBoolean());
        Assert.Equal(1, result.GetProperty("escapedStopCount").GetInt32());
        Assert.Equal(0, result.GetProperty("hostStartedDiagnosticCount").GetInt32());
        Assert.Equal(1, result.GetProperty("middlewareFailureCount").GetInt32());
        Assert.True(result.GetProperty("savedNextRejected").GetBoolean());
        Assert.Equal(0, result.GetProperty("savedTerminalCount").GetInt32());
        Assert.Equal(64, result.GetProperty("concurrentCallerCount").GetInt32());
        Assert.Equal(63, result.GetProperty("rejectedConcurrentCalls").GetInt32());
        Assert.Equal(1, result.GetProperty("concurrentTerminalCount").GetInt32());
        Assert.Equal(1, result.GetProperty("directTerminalCount").GetInt32());
    }

    [Fact]
    public async Task HeadlessReentrantStopFailsFastAndStillCleansUp()
    {
        var result = await RunScenarioAsync("reentrant-stop");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("recursiveFailure").GetBoolean());
        Assert.Equal(1, result.GetProperty("hostedStopCount").GetInt32());
        Assert.Equal("Stopped", result.GetProperty("hostScopeStateAfterStop").GetString());
        Assert.Contains("AUCHOST103", ReadStrings(result, "diagnostics"));
    }

    [Fact]
    public async Task HeadlessUserServicesRunAfterModuleDefaults()
    {
        var result = await RunScenarioAsync("service-ordering");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("deferred").GetBoolean());
        Assert.Equal("UserHeadlessOrderedService", result.GetProperty("resolvedType").GetString());
        Assert.Equal(
            ["ModuleHeadlessOrderedService", "UserHeadlessOrderedService"],
            ReadStrings(result, "registeredTypes"));
        Assert.Equal(
            ["module:pre", "module:configure", "module:post", "user:first", "user:second"],
            ReadStrings(result, "calls"));
    }

    [Fact]
    public async Task HeadlessApplicationContextIsCompleteAndReadableAfterDisposal()
    {
        var result = await RunScenarioAsync("application-context");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal("AtomUI.City.Core.HeadlessApp", result.GetProperty("ApplicationId").GetString());
        Assert.NotEqual(Guid.Empty, result.GetProperty("ApplicationInstanceId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("ApplicationVersion").GetString()));
        Assert.True(Path.IsPathFullyQualified(result.GetProperty("ContentRootPath").GetString()!));
        Assert.True(Path.IsPathFullyQualified(result.GetProperty("AppDataPath").GetString()!));
        Assert.Equal(0, result.GetProperty("startupArgumentCount").GetInt32());
    }

    [Fact]
    public async Task HeadlessGeneratedApplicationRootLoadsOnlyItsDependencyClosure()
    {
        var result = await RunScenarioAsync("generated-modules");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(
            ["GeneratedFoundationModule", "GeneratedApplicationModule"],
            ReadStrings(result, "loadedModules"));
        Assert.Equal(
            [
                "generated:foundation:configure",
                "generated:application:configure",
                "generated:foundation:initialize",
                "generated:application:initialize",
                "generated:application:shutdown",
                "generated:foundation:shutdown",
            ],
            ReadStrings(result, "calls"));
        Assert.Equal(0, result.GetProperty("unusedCreatedCount").GetInt32());
        Assert.True(result.GetProperty("generatedServiceShared").GetBoolean());
        Assert.True(result.GetProperty("generatedServiceLifetimes").GetBoolean());
    }

    private static async Task<JsonElement> RunScenarioAsync(string scenario)
    {
        var result = await RunApplicationAsync("--test-scenario", scenario);
        Assert.True(
            result.ExitCode == 0,
            $"Headless Core scenario '{scenario}' exited with {result.ExitCode}. " +
            $"stdout: {result.StandardOutput} stderr: {result.StandardError}");

        var jsonLine = result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(jsonLine));

        using var document = JsonDocument.Parse(jsonLine);
        return document.RootElement.Clone();
    }

    private static Task<TestProcessResult> RunApplicationAsync(params string[] arguments)
    {
        return RunApplicationAsync(TimeSpan.FromSeconds(15), arguments);
    }

    private static Task<TestProcessResult> RunApplicationAsync(
        TimeSpan timeout,
        params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        var applicationPath = Path.Combine(
            repositoryRoot,
            "output",
            "bin",
            BuildConfiguration,
            "AtomUI.City.Core.HeadlessApp",
            "net10.0",
            "AtomUI.City.Core.HeadlessApp.dll");

        Assert.True(File.Exists(applicationPath), $"Headless fixture was not built: {applicationPath}");
        return ProcessTestRunner.RunAsync(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            repositoryRoot,
            timeout,
            [applicationPath, .. arguments]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AtomUICity.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }

    private static string[] ReadStrings(JsonElement result, string propertyName)
    {
        return result
            .GetProperty(propertyName)
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
    }
}
