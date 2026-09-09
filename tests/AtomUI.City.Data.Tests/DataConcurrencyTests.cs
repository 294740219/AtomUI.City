namespace AtomUI.City.Data.Tests;

public sealed class DataConcurrencyTests
{
    [Fact]
    public async Task AllowConcurrentRunsSameOperationInParallel()
    {
        using var scheduler = new DataOperationScheduler();
        var request = Request(DataConcurrencyPolicy.AllowConcurrent);
        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = scheduler.ExecuteAsync(request, Run);
        var second = scheduler.ExecuteAsync(request, Run);
        await WaitUntilAsync(() => Volatile.Read(ref entered) == 2);
        release.SetResult();

        await Task.WhenAll(first.AsTask(), second.AsTask());
        Assert.Equal(2, entered);

        async ValueTask<DataResult<string>> Run(CancellationToken token)
        {
            Interlocked.Increment(ref entered);
            await release.Task.WaitAsync(token);
            return DataResult<string>.Success("ok");
        }
    }

    [Fact]
    public async Task DisallowConcurrentRejectsSecondOperation()
    {
        using var scheduler = new DataOperationScheduler();
        var request = Request(DataConcurrencyPolicy.DisallowConcurrent);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = scheduler.ExecuteAsync(request, async token =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return DataResult<string>.Success("first");
        });
        await entered.Task;

        var second = await scheduler.ExecuteAsync(
            request,
            _ => ValueTask.FromResult(DataResult<string>.Success("second")));
        release.SetResult();

        Assert.Equal(DataErrorKind.PolicyRejected, second.Error?.Kind);
        Assert.True((await first).Succeeded);
    }

    [Fact]
    public async Task QueuePreservesSubmissionOrder()
    {
        using var scheduler = new DataOperationScheduler();
        var request = Request(DataConcurrencyPolicy.Queue);
        var order = new List<int>();
        var sync = new object();

        var tasks = Enumerable.Range(0, 8)
            .Select(index => scheduler.ExecuteAsync(request, async token =>
            {
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                lock (sync)
                {
                    order.Add(index);
                }

                return DataResult<string>.Success(index.ToString());
            }).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);
        Assert.Equal(Enumerable.Range(0, 8), order);
    }

    [Fact]
    public async Task CancelPreviousCancelsOldOperationAndKeepsNewest()
    {
        using var scheduler = new DataOperationScheduler();
        var request = Request(DataConcurrencyPolicy.CancelPrevious);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = scheduler.ExecuteAsync(request, async token =>
        {
            firstEntered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return DataResult<string>.Success("old");
        });
        await firstEntered.Task;

        var second = await scheduler.ExecuteAsync(
            request,
            _ => ValueTask.FromResult(DataResult<string>.Success("new")));

        Assert.Equal(DataResultStatus.Cancelled, (await first).Status);
        Assert.Equal("new", second.Value);
    }

    [Fact]
    public async Task CancelPreviousCompletionRaceDoesNotUseDisposedCancellationSource()
    {
        using var scheduler = new DataOperationScheduler();
        var request = Request(DataConcurrencyPolicy.CancelPrevious);

        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = scheduler.ExecuteAsync(request, async token =>
            {
                await release.Task.WaitAsync(token);
                return DataResult<string>.Success("old");
            });
            var second = Task.Run(async () =>
            {
                release.TrySetResult();
                return await scheduler.ExecuteAsync(
                    request,
                    _ => ValueTask.FromResult(DataResult<string>.Success("new")));
            });

            await Task.WhenAll(first.AsTask(), second);
        }
    }

    [Fact]
    public async Task LatestWinsSuppressesLateOldResult()
    {
        using var scheduler = new DataOperationScheduler();
        var request = Request(DataConcurrencyPolicy.LatestWins);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = scheduler.ExecuteAsync(request, async _ =>
        {
            await releaseFirst.Task;
            return DataResult<string>.Success("old");
        });
        var second = await scheduler.ExecuteAsync(
            request,
            _ => ValueTask.FromResult(DataResult<string>.Success("new")));
        releaseFirst.SetResult();

        Assert.Equal("new", second.Value);
        Assert.Equal(DataResultStatus.StaleSuppressed, (await first).Status);
    }

    [Theory]
    [InlineData(DataConcurrencyPolicy.AllowConcurrent)]
    [InlineData(DataConcurrencyPolicy.DisallowConcurrent)]
    [InlineData(DataConcurrencyPolicy.LatestWins)]
    [InlineData(DataConcurrencyPolicy.CancelPrevious)]
    public async Task DisposeCancelsActiveOperations(DataConcurrencyPolicy policy)
    {
        var scheduler = new DataOperationScheduler();
        var request = Request(policy);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = scheduler.ExecuteAsync(request, async token =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return DataResult<string>.Success("unexpected");
        });
        await entered.Task;

        scheduler.Dispose();

        Assert.Equal(DataResultStatus.Cancelled, (await operation).Status);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await scheduler.ExecuteAsync(request, _ => ValueTask.FromResult(DataResult<string>.Success("late"))));
    }

    [Fact]
    public async Task KeyedSerialSerializesSameResourceButAllowsDifferentResources()
    {
        using var scheduler = new DataOperationScheduler();
        var sameA = Request(DataConcurrencyPolicy.KeyedSerial, "a");
        var sameA2 = Request(DataConcurrencyPolicy.KeyedSerial, "a");
        var differentB = Request(DataConcurrencyPolicy.KeyedSerial, "b");
        var activeA = 0;
        var maxA = 0;
        var bEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstA = scheduler.ExecuteAsync(sameA, RunA);
        var secondA = scheduler.ExecuteAsync(sameA2, RunA);
        var b = scheduler.ExecuteAsync(differentB, async token =>
        {
            bEntered.SetResult();
            await release.Task.WaitAsync(token);
            return DataResult<string>.Success("b");
        });

        await bEntered.Task;
        release.SetResult();
        await Task.WhenAll(firstA.AsTask(), secondA.AsTask(), b.AsTask());
        Assert.Equal(1, maxA);

        async ValueTask<DataResult<string>> RunA(CancellationToken token)
        {
            var active = Interlocked.Increment(ref activeA);
            maxA = Math.Max(maxA, active);
            await release.Task.WaitAsync(token);
            Interlocked.Decrement(ref activeA);
            return DataResult<string>.Success("a");
        }
    }

    [Fact]
    public async Task StructuredKeysDoNotCollideWhenIdentifiersContainSeparators()
    {
        using var scheduler = new DataOperationScheduler();
        var firstRequest = new DataRequest<string>("client", "operation", DataTransportKind.Http)
        {
            Concurrency = new DataConcurrencyOptions
            {
                Policy = DataConcurrencyPolicy.KeyedSerial,
                OperationKey = "a\u001fb",
                ResourceKey = "c",
            },
        };
        var secondRequest = new DataRequest<string>("client", "operation", DataTransportKind.Http)
        {
            Concurrency = new DataConcurrencyOptions
            {
                Policy = DataConcurrencyPolicy.KeyedSerial,
                OperationKey = "a",
                ResourceKey = "b\u001fc",
            },
        };
        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = scheduler.ExecuteAsync(firstRequest, Run);
        var second = scheduler.ExecuteAsync(secondRequest, Run);
        await WaitUntilAsync(() => Volatile.Read(ref entered) == 2);
        release.TrySetResult();

        await Task.WhenAll(first.AsTask(), second.AsTask());
        Assert.Equal(2, entered);

        async ValueTask<DataResult<string>> Run(CancellationToken token)
        {
            Interlocked.Increment(ref entered);
            await release.Task.WaitAsync(token);
            return DataResult<string>.Success("ok");
        }
    }

    private static DataRequest<string> Request(DataConcurrencyPolicy policy, string? resourceKey = null) =>
        new("client", "operation", DataTransportKind.Http)
        {
            Concurrency = new DataConcurrencyOptions
            {
                Policy = policy,
                ResourceKey = resourceKey,
            },
        };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
