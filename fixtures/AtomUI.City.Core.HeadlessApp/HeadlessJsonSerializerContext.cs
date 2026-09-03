using System.Text.Json.Serialization;

namespace AtomUI.City.Core.HeadlessApp;

internal sealed record GeneratedModulesScenarioResult(
    string Scenario,
    bool Success,
    string[] Calls,
    string[] LoadedModules,
    int UnusedCreatedCount,
    bool GeneratedServiceShared,
    bool GeneratedServiceLifetimes);

internal sealed record StopBeforeStartScenarioResult(
    string Scenario,
    bool Success,
    bool SharedTransaction,
    bool HostScopeCanceled,
    string HostScopeStateAfterStop,
    string HostScopeStateAfterDispose,
    string ChildScopeStateAfterStop,
    int ModuleDisposeCount,
    int ModuleShutdownCount,
    int HostedStartCount,
    int HostedStopCount,
    int StopMiddlewareCount,
    int HostStoppedDiagnosticCount,
    string? OperationId,
    bool StartRejected);

internal sealed record ConfigurationFreezeScenarioResult(
    string Scenario,
    bool Success,
    bool ConfigurationCollectionsWritableBeforeBuild,
    bool ConfigurationCollectionsReadOnlyAfterBuild,
    bool SectionIndexerRejected,
    bool SectionValueRejected,
    bool NestedSectionRejected,
    bool ManagerChildRejected,
    bool LazyChildRejected,
    bool RootIndexerRejected,
    bool RootSectionRejected,
    bool RootChildRejected,
    bool RootReloadRejected,
    bool ProviderSetRejected,
    bool ProviderLoadRejected,
    int CapturedSectionCount,
    int RejectedSectionWriteCount,
    bool OriginalValuesPreserved);

internal sealed record LifecycleScopeDisposeRaceScenarioResult(
    string Scenario,
    bool Success,
    int ChildCount,
    int SynchronousDisposeCount,
    int AsynchronousDisposeCount,
    int DisposedChildCount,
    int RemainingChildCount,
    int CleanupFailureCount,
    string RootStateAfterStop,
    string RootStateAfterDispose,
    bool PublicStopRejected);

internal sealed record ModuleRegistryRaceScenarioResult(
    string Scenario,
    bool Success,
    int ModuleCount,
    int ConcurrentCallerCount,
    string[] ShutdownFirstShutdownOrder,
    string[] ShutdownFirstDisposeOrder,
    bool ShutdownFirstOverlapDetected,
    int DisposeFirstShutdownCount,
    string[] DisposeFirstDisposeOrder,
    bool DisposeFirstOverlapDetected);

internal sealed record ModuleRegistryOwnershipScenarioResult(
    string Scenario,
    bool Success,
    string[] PublicMethods,
    bool DisposalCapabilityHidden,
    bool ImplementationHidden,
    bool ModuleMetadataAvailable);

internal sealed record ModuleGraphCycleScenarioResult(
    string Scenario,
    bool Success,
    int FirstConstructorCount,
    int SecondConstructorCount,
    bool CompleteCyclePathReported);

internal sealed record DiagnosticsContractScenarioResult(
    string Scenario,
    bool Success,
    int InvalidValueRejectionCount,
    int WriterCount,
    int AcceptedWriteCount,
    int RejectedWriteCount,
    bool ConcurrentBoundaryPreserved,
    bool HostStoppedRecordRetained,
    bool WriteAfterHostDisposeRejected,
    bool RecordsReadableAfterHostDispose);

internal sealed record PublicBoundariesScenarioResult(
    string Scenario,
    bool Success,
    bool UnknownStageAreaRejected,
    bool UnknownRootScopeKindRejected,
    bool UnknownChildScopeKindRejected,
    bool InvalidChildWasNotAttached,
    bool UnknownServiceLifetimeRejected,
    bool NullScopedServiceTypeRejected,
    bool NullExposedServiceTypeRejected,
    bool EmptyServiceTypeListsRemainValid,
    bool NullModuleDependencyRejected,
    bool DefaultLifecycleContextStageRejected,
    bool DefaultLifecycleMiddlewareStageRejected,
    bool DefaultDiagnosticStageRejected,
    bool GlobalStageTableImmutable);

internal sealed record SynchronousServiceConfigurationScenarioResult(
    string Scenario,
    bool Success,
    int ConfigurationCount,
    int InitializationCountBeforeStart,
    int InitializationCountAfterStart,
    int SynchronizationContextPostCount);

internal sealed record BuildCleanupFailureScenarioResult(
    string Scenario,
    bool Success,
    string[] FailureMessages,
    int FailingDisposeCount,
    int SuccessfulDisposeCount,
    int FoundationDisposeCount,
    int CleanupDiagnosticCount,
    int ModuleDisposeDiagnosticCount);

internal sealed record BuildAsyncCleanupContextScenarioResult(
    string Scenario,
    bool Success,
    bool ConfigurationBuildReturned,
    string? ConfigurationErrorType,
    string? ConfigurationError,
    int ConfigurationSynchronizationContextPostCount,
    int ConfigurationDisposeCount,
    bool ConstructionBuildReturned,
    string? ConstructionErrorType,
    string? ConstructionError,
    int ConstructionSynchronizationContextPostCount,
    int ConstructionDisposeCount);

internal sealed record GenericHostAsyncCleanupScenarioResult(
    string Scenario,
    bool Success,
    bool BuildReturned,
    string? ErrorType,
    string? Error,
    int SynchronizationContextPostCount,
    int DisposeCount,
    bool DisposeCompleted,
    int CleanupDiagnosticCount);

internal sealed record BuildAsyncCleanupTimeoutScenarioResult(
    string Scenario,
    bool Success,
    bool BuildReturned,
    string? ErrorType,
    int SynchronizationContextPostCount,
    int DisposeCount,
    bool IncompleteAtTimeout,
    bool CompletedAfterRelease,
    string? CleanupTimeout,
    string? CleanupMayStillBeRunning);

internal sealed record LifecycleNextOwnershipScenarioResult(
    string Scenario,
    bool Success,
    bool StartStayedPending,
    bool FireAndForgetRejected,
    int HostedStartCount,
    int HostedStopCount,
    bool StopStayedPending,
    bool StopFireAndForgetRejected,
    int EscapedStopCount,
    int HostStartedDiagnosticCount,
    int MiddlewareFailureCount,
    bool SavedNextRejected,
    int SavedTerminalCount,
    int ConcurrentCallerCount,
    int RejectedConcurrentCalls,
    int ConcurrentTerminalCount,
    int DirectTerminalCount);

internal sealed record LifecycleScenarioResult(
    string Scenario,
    bool Success,
    string[] Calls,
    int HostedStartCount,
    int HostedStopCount,
    bool? ScopedDisposed,
    string HostScopeState,
    string? ApplicationScopeState,
    string[] Diagnostics);

internal sealed record StartupFailureScenarioResult(
    string Scenario,
    bool Success,
    string? ErrorType,
    string? Error,
    string[] Calls,
    string HostScopeState,
    string[] Diagnostics);

internal sealed record StartupRollbackFailureScenarioResult(
    string Scenario,
    bool Success,
    string[] FailureMessages,
    string[] Calls,
    int RollbackDiagnosticCount,
    string HostScopeState);

internal sealed record ShutdownFailureScenarioResult(
    string Scenario,
    bool Success,
    int? FailureCount,
    string[] Calls,
    int HostedStopCount,
    string HostScopeState,
    string[] Diagnostics);

internal sealed record LifecycleDiagnosticsScenarioResult(
    string Scenario,
    bool Success,
    string? Stage,
    string? MiddlewareType,
    string? OperationId,
    string? SummaryOperationId,
    string? ExceptionType,
    int HostedStopCount,
    string HostScopeStateAfterStop);

internal sealed record RunCancellationScenarioResult(
    string Scenario,
    bool Success,
    bool Canceled,
    string[] Calls,
    int HostedStopCount,
    string HostScopeState);

internal sealed record ConcurrentStopScenarioResult(
    string Scenario,
    bool Success,
    bool SharedTransaction,
    int ShutdownCount,
    string HostScopeState);

internal sealed record ReentrantStopScenarioResult(
    string Scenario,
    bool Success,
    bool RecursiveFailure,
    int HostedStopCount,
    string HostScopeStateAfterStop,
    string[] Diagnostics);

internal sealed record ServiceOrderingScenarioResult(
    string Scenario,
    bool Success,
    bool Deferred,
    string ResolvedType,
    string[] RegisteredTypes,
    string[] Calls);

internal sealed record ApplicationContextScenarioResult(
    string Scenario,
    bool Success,
    [property: JsonPropertyName("ApplicationId")] string ApplicationId,
    [property: JsonPropertyName("ApplicationInstanceId")] Guid ApplicationInstanceId,
    [property: JsonPropertyName("ApplicationName")] string ApplicationName,
    [property: JsonPropertyName("ApplicationVersion")] string ApplicationVersion,
    [property: JsonPropertyName("EnvironmentName")] string EnvironmentName,
    [property: JsonPropertyName("ContentRootPath")] string ContentRootPath,
    [property: JsonPropertyName("AppDataPath")] string AppDataPath,
    int StartupArgumentCount);

internal sealed record ScenarioFailureResult(
    string Scenario,
    bool Success,
    string? ErrorType,
    string Error);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GeneratedModulesScenarioResult))]
[JsonSerializable(typeof(StopBeforeStartScenarioResult))]
[JsonSerializable(typeof(ConfigurationFreezeScenarioResult))]
[JsonSerializable(typeof(LifecycleScopeDisposeRaceScenarioResult))]
[JsonSerializable(typeof(ModuleRegistryRaceScenarioResult))]
[JsonSerializable(typeof(ModuleRegistryOwnershipScenarioResult))]
[JsonSerializable(typeof(ModuleGraphCycleScenarioResult))]
[JsonSerializable(typeof(DiagnosticsContractScenarioResult))]
[JsonSerializable(typeof(PublicBoundariesScenarioResult))]
[JsonSerializable(typeof(SynchronousServiceConfigurationScenarioResult))]
[JsonSerializable(typeof(BuildCleanupFailureScenarioResult))]
[JsonSerializable(typeof(BuildAsyncCleanupContextScenarioResult))]
[JsonSerializable(typeof(GenericHostAsyncCleanupScenarioResult))]
[JsonSerializable(typeof(BuildAsyncCleanupTimeoutScenarioResult))]
[JsonSerializable(typeof(LifecycleNextOwnershipScenarioResult))]
[JsonSerializable(typeof(LifecycleScenarioResult))]
[JsonSerializable(typeof(StartupFailureScenarioResult))]
[JsonSerializable(typeof(StartupRollbackFailureScenarioResult))]
[JsonSerializable(typeof(ShutdownFailureScenarioResult))]
[JsonSerializable(typeof(LifecycleDiagnosticsScenarioResult))]
[JsonSerializable(typeof(RunCancellationScenarioResult))]
[JsonSerializable(typeof(ConcurrentStopScenarioResult))]
[JsonSerializable(typeof(ReentrantStopScenarioResult))]
[JsonSerializable(typeof(ServiceOrderingScenarioResult))]
[JsonSerializable(typeof(ApplicationContextScenarioResult))]
[JsonSerializable(typeof(ScenarioFailureResult))]
internal sealed partial class HeadlessJsonSerializerContext : JsonSerializerContext;
