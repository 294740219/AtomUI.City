namespace AtomUI.City.Data.Tests;

public sealed class DataCacheConsistencyTests
{
    [Fact]
    public async Task ExpiredEntryIsRemovedOnRead()
    {
        var time = new ManualTimeProvider();
        var cache = new InMemoryDataRequestCache(timeProvider: time);
        var key = CreateKey("plugin-a", "principal-a");

        await cache.SetAsync(
            key,
            "value",
            new DataCacheEntryOptions { TimeToLive = TimeSpan.FromSeconds(5) });
        time.Advance(TimeSpan.FromSeconds(6));

        var lookup = await cache.TryGetAsync<string>(key);

        Assert.False(lookup.IsHit);
    }

    [Fact]
    public async Task PluginInvalidationRemovesOnlyMatchingContribution()
    {
        var cache = new InMemoryDataRequestCache();
        var pluginA = CreateKey("plugin-a", "principal-a");
        var pluginB = CreateKey("plugin-b", "principal-a");
        await cache.SetAsync(pluginA, "a");
        await cache.SetAsync(pluginB, "b");

        var result = await cache.InvalidateAsync(DataCacheInvalidation.ForPlugin("plugin-a"));

        Assert.Equal(1, result.RemovedEntryCount);
        Assert.False((await cache.TryGetAsync<string>(pluginA)).IsHit);
        Assert.True((await cache.TryGetAsync<string>(pluginB)).IsHit);
    }

    [Fact]
    public async Task PrincipalInvalidationDoesNotCrossAccountBoundary()
    {
        var cache = new InMemoryDataRequestCache();
        var principalA = CreateKey("plugin-a", "principal-a");
        var principalB = CreateKey("plugin-a", "principal-b");
        await cache.SetAsync(principalA, "a");
        await cache.SetAsync(principalB, "b");

        await cache.InvalidateAsync(DataCacheInvalidation.ForPrincipal("principal-a"));

        Assert.False((await cache.TryGetAsync<string>(principalA)).IsHit);
        Assert.True((await cache.TryGetAsync<string>(principalB)).IsHit);
    }

    [Fact]
    public async Task ExactInvalidationReportsTheActualRemovalCount()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var cache = new InMemoryDataRequestCache(diagnostics);
        var missing = CreateKey("plugin-a", "principal-a");

        await cache.InvalidateAsync(missing);

        var record = Assert.Single(diagnostics.Records);
        Assert.Equal(DataDiagnosticIds.CacheInvalidated, record.Code);
        Assert.Contains("removed 0 entries", record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyInvalidationRejectsNullElements()
    {
        var key = CreateKey("plugin-a", "principal-a");

        Assert.Throws<ArgumentException>(() => DataCacheInvalidation.Keys([key, null!]));
    }

    [Fact]
    public async Task TargetedInvalidationSupportsAllDeclaredRevisionDimensions()
    {
        var baseline = CreateKey("plugin-a", "principal-a");

        await AssertInvalidatesOnlyMatching(
            baseline with { OperationName = "target" },
            baseline with { OperationName = "other" },
            DataCacheInvalidation.ForOperation("catalog", "target"));
        await AssertInvalidatesOnlyMatching(
            baseline with { PermissionRevision = "permission-v1" },
            baseline with { PermissionRevision = "permission-v2" },
            DataCacheInvalidation.ForPermissionRevision("permission-v1"));
        await AssertInvalidatesOnlyMatching(
            baseline with { ClientVersion = "client-v1" },
            baseline with { ClientVersion = "client-v2" },
            DataCacheInvalidation.ForClientVersion("catalog", "client-v1"));
        await AssertInvalidatesOnlyMatching(
            baseline with { PolicyVersion = "policy-v1" },
            baseline with { PolicyVersion = "policy-v2" },
            DataCacheInvalidation.ForPolicyVersion("policy-v1"));
    }

    [Fact]
    public async Task InvalidationPreventsInflightQueryFromRepopulatingStaleCache()
    {
        var cache = new InMemoryDataRequestCache();
        var transport = new DelayedSuccessfulTransport();
        using var pipeline = new DataRequestPipeline(transport, cache: cache);
        var request = new DataRequest<string>("catalog", "get-items", DataTransportKind.Http)
        {
            Cache = DataCacheOptions.Enabled("fingerprint"),
        };

        var pending = pipeline.SendAsync(request).AsTask();
        await transport.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cache.InvalidateAsync(DataCacheInvalidation.ForClient("catalog", DataCacheInvalidationReason.Mutation));
        transport.Release.TrySetResult();

        var result = await pending.WaitAsync(TimeSpan.FromSeconds(2));
        var cacheKey = DataCacheKey.Create(request, DataAuthenticationMode.Anonymous.ToString());
        Assert.True(result.Succeeded);
        Assert.False((await cache.TryGetAsync<string>(cacheKey)).IsHit);
    }

    [Fact]
    public void CanonicalFingerprintIncludesEndpointMethodAndPayloadBoundaries()
    {
        var baseline = DataCacheFingerprint.Create("https://api.example.test/items", "get", "ab:c");

        Assert.Equal(baseline, DataCacheFingerprint.Create("https://api.example.test/items", "GET", "ab:c"));
        Assert.NotEqual(baseline, DataCacheFingerprint.Create("https://api.example.test/item", "GET", "sab:c"));
        Assert.NotEqual(baseline, DataCacheFingerprint.Create("https://api.example.test/items", "POST", "ab:c"));
        Assert.NotEqual(baseline, DataCacheFingerprint.Create("https://api.example.test/items", "GET", "a:bc"));
    }

    [Fact]
    public async Task PartialOptimisticApplyFailureIsRolledBackBeforeReturning()
    {
        var optimistic = new RecordingOptimisticUpdate(throwDuringApply: true);
        var transport = new SuccessfulTransport();
        using var pipeline = new DataRequestPipeline(transport);
        var request = new DataRequest<string>("catalog", "save", DataTransportKind.Http, DataAccessMode.Mutation)
        {
            Consistency = new DataConsistencyOptions { OptimisticUpdate = optimistic },
        };

        var result = await pipeline.SendAsync(request);

        Assert.Equal(DataErrorKind.LocalStorageError, result.Error?.Kind);
        Assert.Equal(1, optimistic.ApplyCount);
        Assert.Equal(1, optimistic.RollBackCount);
        Assert.Equal(0, optimistic.ConfirmCount);
        Assert.Equal(0, transport.CallCount);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task CancellationHonorsOptimisticRollbackPolicy(bool rollbackOnCancellation, int expectedRollbacks)
    {
        var optimistic = new RecordingOptimisticUpdate();
        var transport = new BlockingTransport();
        using var pipeline = new DataRequestPipeline(transport);
        using var cancellation = new CancellationTokenSource();
        var request = new DataRequest<string>("catalog", "save", DataTransportKind.Http, DataAccessMode.Mutation)
        {
            Consistency = new DataConsistencyOptions
            {
                OptimisticUpdate = optimistic,
                RollBackOnCancellation = rollbackOnCancellation,
            },
        };

        var pending = pipeline.SendAsync(request, cancellation.Token).AsTask();
        await transport.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(DataResultStatus.Cancelled, result.Status);
        Assert.Equal(expectedRollbacks, optimistic.RollBackCount);
    }

    [Fact]
    public async Task SuccessfulMutationConfirmsOptimisticUpdateAndInvalidatesCache()
    {
        var cache = new InMemoryDataRequestCache();
        var key = CreateKey("plugin-a", "principal-a");
        await cache.SetAsync(key, "stale");
        var optimistic = new RecordingOptimisticUpdate();
        var transport = new SuccessfulTransport();
        using var pipeline = new DataRequestPipeline(transport, cache: cache);
        var request = new DataRequest<string>("catalog", "save", DataTransportKind.Http, DataAccessMode.Mutation)
        {
            Consistency = new DataConsistencyOptions
            {
                OptimisticUpdate = optimistic,
                InvalidationsOnSuccess = [DataCacheInvalidation.ForClient("catalog", DataCacheInvalidationReason.Mutation)],
            },
        };

        var result = await pipeline.SendAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(1, optimistic.ApplyCount);
        Assert.Equal(1, optimistic.ConfirmCount);
        Assert.Equal(0, optimistic.RollBackCount);
        Assert.False((await cache.TryGetAsync<string>(key)).IsHit);
    }

    private static DataCacheKey CreateKey(string plugin, string principal) => new(
        "catalog",
        "get-items",
        DataTransportKind.Http,
        DataAccessMode.Query,
        "fingerprint",
        "Bearer",
        principal,
        "permission-v1",
        plugin,
        "client-v1",
        "policy-v1");

    private static async Task AssertInvalidatesOnlyMatching(
        DataCacheKey matching,
        DataCacheKey other,
        DataCacheInvalidation invalidation)
    {
        var cache = new InMemoryDataRequestCache();
        await cache.SetAsync(matching, "matching");
        await cache.SetAsync(other, "other");

        var result = await cache.InvalidateAsync(invalidation);

        Assert.Equal(1, result.RemovedEntryCount);
        Assert.False((await cache.TryGetAsync<string>(matching)).IsHit);
        Assert.True((await cache.TryGetAsync<string>(other)).IsHit);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class RecordingOptimisticUpdate(bool throwDuringApply = false) : IDataOptimisticUpdate
    {
        public int ApplyCount { get; private set; }

        public int ConfirmCount { get; private set; }

        public int RollBackCount { get; private set; }

        public ValueTask ApplyAsync(DataRequestContext context, CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            return throwDuringApply
                ? ValueTask.FromException(new InvalidOperationException("partially applied"))
                : ValueTask.CompletedTask;
        }

        public ValueTask ConfirmAsync(DataRequestContext context, CancellationToken cancellationToken = default)
        {
            ConfirmCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask RollBackAsync(DataRequestContext context, CancellationToken cancellationToken = default)
        {
            RollBackCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SuccessfulTransport : IRequestResponseTransport
    {
        public int CallCount { get; private set; }

        public DataTransportKind Kind => DataTransportKind.Http;

        public ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(DataResult<string>.Success("saved").Cast<TResponse>());
        }
    }

    private sealed class BlockingTransport : IRequestResponseTransport
    {
        public TaskCompletionSource Entered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public DataTransportKind Kind => DataTransportKind.Http;

        public async ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The blocking transport unexpectedly completed.");
        }
    }

    private sealed class DelayedSuccessfulTransport : IRequestResponseTransport
    {
        public TaskCompletionSource Entered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public DataTransportKind Kind => DataTransportKind.Http;

        public async ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return DataResult<string>.Success("fresh").Cast<TResponse>();
        }
    }
}
