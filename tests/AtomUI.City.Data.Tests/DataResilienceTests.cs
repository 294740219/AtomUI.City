using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Data.Tests;

public sealed class DataResilienceTests
{
    [Fact]
    public async Task CircuitOpensAfterConfiguredTransientFailures()
    {
        var transport = new CountingTransport(DataResult<string>.Failed(
            new DataError(DataErrorKind.ServiceUnavailable, "offline")));
        using var pipeline = new DataRequestPipeline(transport);
        var request = Request(new DataResilienceOptions
        {
            CircuitBreaker = new DataCircuitBreakerOptions
            {
                IsEnabled = true,
                FailureThreshold = 2,
                BreakDuration = TimeSpan.FromMinutes(1),
            },
        });

        await pipeline.SendAsync(request);
        await pipeline.SendAsync(request);
        var rejected = await pipeline.SendAsync(request);

        Assert.Equal(2, transport.CallCount);
        Assert.Equal(DataErrorKind.ServiceUnavailable, rejected.Error?.Kind);
        Assert.Contains("circuit", rejected.Error?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RateLimitRejectsCallsPastWindowLimit()
    {
        var transport = new CountingTransport(DataResult<string>.Success("ok"));
        using var pipeline = new DataRequestPipeline(transport);
        var request = Request(new DataResilienceOptions
        {
            RateLimit = new DataRateLimitOptions
            {
                IsEnabled = true,
                PermitLimit = 1,
                Window = TimeSpan.FromMinutes(1),
            },
        });

        var first = await pipeline.SendAsync(request);
        var second = await pipeline.SendAsync(request);

        Assert.True(first.Succeeded);
        Assert.Equal(DataErrorKind.PolicyRejected, second.Error?.Kind);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task ExplicitFallbackReplacesFinalTransientFailure()
    {
        var transport = new CountingTransport(DataResult<string>.Failed(
            new DataError(DataErrorKind.NetworkUnavailable, "offline")));
        using var pipeline = new DataRequestPipeline(
            transport,
            fallbackProvider: new StaticFallbackProvider("cached"));
        var request = Request(new DataResilienceOptions { EnableFallback = true });

        var result = await pipeline.SendAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal("cached", result.Value);
    }

    [Fact]
    public async Task FallbackNeverMasksAuthorizationFailure()
    {
        var transport = new CountingTransport(DataResult<string>.Failed(
            new DataError(DataErrorKind.AuthorizationForbidden, "denied")));
        var fallback = new StaticFallbackProvider("forbidden-cache");
        using var pipeline = new DataRequestPipeline(transport, fallbackProvider: fallback);

        var result = await pipeline.SendAsync(Request(new DataResilienceOptions { EnableFallback = true }));

        Assert.Equal(DataErrorKind.AuthorizationForbidden, result.Error?.Kind);
        Assert.Equal(0, fallback.CallCount);
    }

    [Fact]
    public async Task CancelledHalfOpenProbeDoesNotPermanentlyBlockCircuit()
    {
        var transport = new HalfOpenCancellationTransport();
        using var pipeline = new DataRequestPipeline(transport);
        var request = Request(new DataResilienceOptions
        {
            CircuitBreaker = new DataCircuitBreakerOptions
            {
                IsEnabled = true,
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromMilliseconds(30),
            },
        });

        var first = await pipeline.SendAsync(request);
        Assert.Equal(DataErrorKind.ServiceUnavailable, first.Error?.Kind);

        await Task.Delay(80);
        using var cancellation = new CancellationTokenSource();
        var probe = pipeline.SendAsync(request, cancellation.Token).AsTask();
        await transport.ProbeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var cancelled = await probe;
        var recovered = await pipeline.SendAsync(request);

        Assert.Equal(DataResultStatus.Cancelled, cancelled.Status);
        Assert.True(recovered.Succeeded);
        Assert.Equal("recovered", recovered.Value);
        Assert.Equal(3, transport.CallCount);
    }

    [Theory]
    [InlineData(DataResiliencePolicyScope.Operation, true)]
    [InlineData(DataResiliencePolicyScope.Client, false)]
    [InlineData(DataResiliencePolicyScope.Global, false)]
    public async Task CircuitScopeControlsFailureIsolation(
        DataResiliencePolicyScope scope,
        bool secondOperationCanExecute)
    {
        var transport = new OperationAwareTransport();
        using var pipeline = new DataRequestPipeline(transport);
        var resilience = new DataResilienceOptions
        {
            Scope = scope,
            CircuitBreaker = new DataCircuitBreakerOptions
            {
                IsEnabled = true,
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromMinutes(1),
            },
        };

        await pipeline.SendAsync(new DataRequest<string>("client-a", "fail", DataTransportKind.Http)
        {
            Resilience = resilience,
        });
        var second = await pipeline.SendAsync(new DataRequest<string>("client-a", "succeed", DataTransportKind.Http)
        {
            Resilience = resilience,
        });

        Assert.Equal(secondOperationCanExecute, second.Succeeded);
        Assert.Equal(secondOperationCanExecute ? 2 : 1, transport.CallCount);
    }

    [Fact]
    public async Task GlobalCircuitScopeCrossesClientBoundary()
    {
        var transport = new OperationAwareTransport();
        using var pipeline = new DataRequestPipeline(transport);
        var resilience = new DataResilienceOptions
        {
            Scope = DataResiliencePolicyScope.Global,
            CircuitBreaker = new DataCircuitBreakerOptions
            {
                IsEnabled = true,
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromMinutes(1),
            },
        };

        await pipeline.SendAsync(new DataRequest<string>("client-a", "fail", DataTransportKind.Http)
        {
            Resilience = resilience,
        });
        var otherClient = await pipeline.SendAsync(new DataRequest<string>("client-b", "succeed", DataTransportKind.Http)
        {
            Resilience = resilience,
        });

        Assert.Equal(DataErrorKind.ServiceUnavailable, otherClient.Error?.Kind);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task FallbackFailurePreservesTransportFailureAndWritesDiagnostic()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var transport = new CountingTransport(DataResult<string>.Failed(
            new DataError(DataErrorKind.NetworkUnavailable, "offline")));
        using var pipeline = new DataRequestPipeline(
            transport,
            diagnostics: diagnostics,
            fallbackProvider: new ThrowingFallbackProvider());

        var result = await pipeline.SendAsync(Request(new DataResilienceOptions { EnableFallback = true }));

        Assert.Equal(DataErrorKind.NetworkUnavailable, result.Error?.Kind);
        Assert.Contains(diagnostics.Records, record => record.Code == DataDiagnosticIds.FallbackFailed);
    }

    [Fact]
    public async Task OpenCircuitStillAllowsCacheHitWithoutTransport()
    {
        var cache = new InMemoryDataRequestCache();
        var transport = new CountingTransport(DataResult<string>.Failed(
            new DataError(DataErrorKind.ServiceUnavailable, "offline")));
        using var pipeline = new DataRequestPipeline(transport, cache: cache);
        var resilience = new DataResilienceOptions
        {
            CircuitBreaker = new DataCircuitBreakerOptions
            {
                IsEnabled = true,
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromMinutes(1),
            },
        };
        await pipeline.SendAsync(Request(resilience));
        var cachedRequest = new DataRequest<string>("resilience", "load", DataTransportKind.Http)
        {
            Resilience = resilience,
            Cache = DataCacheOptions.Enabled("cached-request"),
        };
        var cacheKey = DataCacheKey.Create(cachedRequest, DataAuthenticationMode.Anonymous.ToString());
        await cache.SetAsync(cacheKey, "cached");

        var result = await pipeline.SendAsync(cachedRequest);

        Assert.True(result.Succeeded);
        Assert.Equal("cached", result.Value);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task CircuitRejectionCanUseExplicitFallback()
    {
        var fallback = new StaticFallbackProvider("cached");
        var transport = new CountingTransport(DataResult<string>.Failed(
            new DataError(DataErrorKind.ServiceUnavailable, "offline")));
        using var pipeline = new DataRequestPipeline(transport, fallbackProvider: fallback);
        var request = Request(new DataResilienceOptions
        {
            EnableFallback = true,
            CircuitBreaker = new DataCircuitBreakerOptions
            {
                IsEnabled = true,
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromMinutes(1),
            },
        });

        var first = await pipeline.SendAsync(request);
        var second = await pipeline.SendAsync(request);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal("cached", second.Value);
        Assert.Equal(1, transport.CallCount);
        Assert.Equal(2, fallback.CallCount);
    }

    [Fact]
    public async Task CredentialFailureDoesNotConsumeRateLimitPermit()
    {
        var transport = new CountingTransport(DataResult<string>.Success("ok"));
        using var pipeline = new DataRequestPipeline(transport);
        var resilience = new DataResilienceOptions
        {
            RateLimit = new DataRateLimitOptions
            {
                IsEnabled = true,
                PermitLimit = 1,
                Window = TimeSpan.FromMinutes(1),
            },
        };
        var secured = new DataRequest<string>("resilience", "load", DataTransportKind.Http)
        {
            Authentication = DataAuthenticationOptions.Bearer(),
            Resilience = resilience,
        };

        var credentialFailure = await pipeline.SendAsync(secured);
        var admitted = await pipeline.SendAsync(Request(resilience));

        Assert.Equal(DataErrorKind.CredentialUnavailable, credentialFailure.Error?.Kind);
        Assert.True(admitted.Succeeded);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task ParentScopeStopSuppressesFallbackFromOpenCircuit()
    {
        var fallback = new BlockingSecondFallbackProvider();
        var transport = new CountingTransport(DataResult<string>.Failed(
            new DataError(DataErrorKind.ServiceUnavailable, "offline")));
        using var pipeline = new DataRequestPipeline(transport, fallbackProvider: fallback);
        var resilience = new DataResilienceOptions
        {
            EnableFallback = true,
            CircuitBreaker = new DataCircuitBreakerOptions
            {
                IsEnabled = true,
                FailureThreshold = 1,
                BreakDuration = TimeSpan.FromMinutes(1),
            },
        };

        await pipeline.SendAsync(Request(resilience));
        var parent = LifecycleScope.CreateRoot(LifecycleScopeKind.Route, "fallback-route");
        var request = new DataRequest<string>("resilience", "load", DataTransportKind.Http)
        {
            Resilience = resilience,
            ParentScope = parent,
        };
        var pending = pipeline.SendAsync(request).AsTask();
        await fallback.SecondCallEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await parent.StopAsync();
        fallback.FinishSecondCall.TrySetResult();
        var result = await pending;
        await parent.DisposeAsync();

        Assert.Equal(DataResultStatus.StaleSuppressed, result.Status);
        Assert.Equal(1, transport.CallCount);
    }

    private static DataRequest<string> Request(DataResilienceOptions resilience) =>
        new("resilience", "load", DataTransportKind.Http) { Resilience = resilience };

    private sealed class CountingTransport(DataResult<string> response) : IRequestResponseTransport
    {
        public int CallCount { get; private set; }

        public DataTransportKind Kind => DataTransportKind.Http;

        public ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(response.Cast<TResponse>());
        }
    }

    private sealed class StaticFallbackProvider(string value) : IDataFallbackProvider
    {
        public int CallCount { get; private set; }

        public ValueTask<DataFallbackResult<TResponse>> TryGetFallbackAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            DataError error,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var result = value is TResponse typed
                ? DataFallbackResult<TResponse>.FromResult(DataResult<TResponse>.Success(typed))
                : DataFallbackResult<TResponse>.None();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class HalfOpenCancellationTransport : IRequestResponseTransport
    {
        private int _callCount;

        public TaskCompletionSource ProbeStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount => Volatile.Read(ref _callCount);

        public DataTransportKind Kind => DataTransportKind.Http;

        public async ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                return DataResult<string>.Failed(
                    new DataError(DataErrorKind.ServiceUnavailable, "offline"))
                    .Cast<TResponse>();
            }

            if (call == 2)
            {
                ProbeStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return DataResult<string>.Success("recovered").Cast<TResponse>();
        }
    }

    private sealed class OperationAwareTransport : IRequestResponseTransport
    {
        public int CallCount { get; private set; }

        public DataTransportKind Kind => DataTransportKind.Http;

        public ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var result = request.OperationName == "fail"
                ? DataResult<string>.Failed(new DataError(DataErrorKind.ServiceUnavailable, "offline"))
                : DataResult<string>.Success("ok");
            return ValueTask.FromResult(result.Cast<TResponse>());
        }
    }

    private sealed class ThrowingFallbackProvider : IDataFallbackProvider
    {
        public ValueTask<DataFallbackResult<TResponse>> TryGetFallbackAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            DataError error,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("fallback failed");
    }

    private sealed class BlockingSecondFallbackProvider : IDataFallbackProvider
    {
        private int _callCount;

        public TaskCompletionSource SecondCallEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FinishSecondCall { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<DataFallbackResult<TResponse>> TryGetFallbackAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            DataError error,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                return DataFallbackResult<TResponse>.None();
            }

            SecondCallEntered.TrySetResult();
            await FinishSecondCall.Task;
            return DataFallbackResult<TResponse>.FromResult(
                DataResult<string>.Success("late fallback").Cast<TResponse>());
        }
    }
}
