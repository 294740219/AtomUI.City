using System.Runtime.CompilerServices;
using AtomUI.City.Core.Lifecycle;
using Grpc.Core;

namespace AtomUI.City.Data.Tests;

public sealed class DataStreamingTests
{
    [Fact]
    public async Task FullBufferDoesNotPreventTerminalFailureFromCompleting()
    {
        await using var stream = DataStream<int>.Create(
            FailAfterOne(),
            new DataStreamOptions
            {
                Capacity = 1,
                BackpressurePolicy = DataBackpressurePolicy.Buffer,
            });

        await stream.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        var results = await ReadAllAsync(stream);

        var failure = Assert.Single(results);
        Assert.Equal(DataResultStatus.Failed, failure.Status);
        Assert.Equal(DataErrorKind.StreamProtocolError, failure.Error?.Kind);
    }

    [Fact]
    public async Task DropOldestRetainsNewestValuesWithoutBlockingProducer()
    {
        await using var stream = DataStream<int>.Create(
            Values(1, 2, 3, 4),
            new DataStreamOptions
            {
                Capacity = 2,
                BackpressurePolicy = DataBackpressurePolicy.DropOldest,
            });
        await stream.Completion;

        var results = await ReadAllAsync(stream);

        Assert.Equal([3, 4], results.Select(static result => result.Value));
    }

    [Fact]
    public async Task ParentScopeStopCancelsStreamAndCompletes()
    {
        var scope = LifecycleScope.CreateRoot(LifecycleScopeKind.Operation, "data-stream-test");
        await using var stream = DataStream<int>.Create(
            WaitForever(),
            new DataStreamOptions { ParentScope = scope });

        await scope.StopAsync();
        await stream.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(stream.Completion.IsCompletedSuccessfully);
        await scope.DisposeAsync();
    }

    [Fact]
    public async Task StreamRejectsSecondConsumer()
    {
        await using var stream = DataStream<int>.Create(Values(1));
        await stream.Completion;
        await using var first = stream.GetAsyncEnumerator();
        Assert.True(await first.MoveNextAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in stream)
            {
            }
        });
    }

    [Fact]
    public async Task ErrorMapperFailureStillProducesTerminalDataError()
    {
        await using var stream = DataStream<int>.Create(
            FailAfterOne(),
            errorMapper: _ => throw new InvalidOperationException("mapper failed"));

        await stream.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        var results = await ReadAllAsync(stream);

        var failure = Assert.Single(results, static result => result.Status == DataResultStatus.Failed);
        Assert.Equal(DataErrorKind.StreamProtocolError, failure.Error?.Kind);
        Assert.IsType<AggregateException>(failure.Error?.Exception);
    }

    [Fact]
    public async Task ConcurrentDisposeCallsShareCleanupTransaction()
    {
        var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = DataStream<int>.Create(WaitWithCleanup(cleanupEntered, finishCleanup));

        var first = stream.DisposeAsync().AsTask();
        await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = stream.DisposeAsync().AsTask();

        Assert.Same(first, second);
        Assert.False(second.IsCompleted);
        finishCleanup.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task OwnedStreamReleaseFailureFaultsCompletionAndDispose()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var stream = DataStream<int>.CreateOwned(
            Values(1),
            DataStreamOptions.Default,
            diagnostics,
            errorMapper: null,
            () => ValueTask.FromException(new InvalidOperationException("release failed")));

        var completionFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => stream.Completion);
        var disposeFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => stream.DisposeAsync().AsTask());

        Assert.Equal("release failed", completionFailure.Message);
        Assert.Equal("release failed", disposeFailure.Message);
        Assert.Contains(diagnostics.Records, record => record.Code == DataDiagnosticIds.StreamFailed);
    }

    [Fact]
    public async Task DuplexConcurrentDisposeSharesCleanupAndAlwaysDisposesCall()
    {
        var reader = new BlockingStreamReader();
        var callDisposed = 0;
        var call = new AsyncDuplexStreamingCall<TestMessage, TestMessage>(
            new NoOpClientStreamWriter(),
            reader,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => Interlocked.Increment(ref callDisposed));
        var stream = new GrpcDuplexStream<TestMessage, TestMessage>(
            call,
            DataStreamOptions.Default,
            diagnostics: null,
            CancellationToken.None);
        await reader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var first = stream.DisposeAsync().AsTask();
        await reader.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = stream.DisposeAsync().AsTask();

        Assert.Same(first, second);
        Assert.False(second.IsCompleted);
        reader.FinishCleanup.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, callDisposed);
    }

    [Fact]
    public async Task ClientStreamDisposeWaitsForActiveWriteAndSharesTransaction()
    {
        var writer = new BlockingClientStreamWriter();
        var callDisposed = 0;
        var call = new AsyncClientStreamingCall<TestMessage, TestMessage>(
            writer,
            Task.FromResult(new TestMessage()),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => Interlocked.Increment(ref callDisposed));
        var stream = new GrpcClientStream<TestMessage, TestMessage>(call);

        var write = stream.WriteAsync(new TestMessage()).AsTask();
        await writer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var firstDispose = stream.DisposeAsync().AsTask();
        var secondDispose = stream.DisposeAsync().AsTask();

        Assert.Same(firstDispose, secondDispose);
        Assert.False(firstDispose.IsCompleted);
        Assert.Equal(0, callDisposed);
        writer.Finish.TrySetResult();
        await write.WaitAsync(TimeSpan.FromSeconds(2));
        await firstDispose.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, callDisposed);
    }

    [Fact]
    public async Task DuplexDisposeWaitsForActiveWriteBeforeReleasingCall()
    {
        var writer = new BlockingClientStreamWriter();
        var reader = new BlockingStreamReader();
        var callDisposed = 0;
        var call = new AsyncDuplexStreamingCall<TestMessage, TestMessage>(
            writer,
            reader,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => Interlocked.Increment(ref callDisposed));
        var stream = new GrpcDuplexStream<TestMessage, TestMessage>(
            call,
            DataStreamOptions.Default,
            diagnostics: null,
            CancellationToken.None);
        await reader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var write = stream.WriteAsync(new TestMessage()).AsTask();
        await writer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var dispose = stream.DisposeAsync().AsTask();

        Assert.False(dispose.IsCompleted);
        Assert.Equal(0, callDisposed);
        writer.Finish.TrySetResult();
        await write.WaitAsync(TimeSpan.FromSeconds(2));
        await reader.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        reader.FinishCleanup.TrySetResult();
        await dispose.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, callDisposed);
    }

    private static async Task<List<DataResult<int>>> ReadAllAsync(IDataStream<int> stream)
    {
        var results = new List<DataResult<int>>();
        await foreach (var result in stream)
        {
            results.Add(result);
        }

        return results;
    }

    private static async IAsyncEnumerable<int> FailAfterOne()
    {
        yield return 1;
        await Task.Yield();
        throw new InvalidOperationException("stream failed");
    }

    private static async IAsyncEnumerable<int> Values(params int[] values)
    {
        foreach (var value in values)
        {
            yield return value;
        }

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<int> WaitForever(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        yield break;
    }

    private static async IAsyncEnumerable<int> WaitWithCleanup(
        TaskCompletionSource cleanupEntered,
        TaskCompletionSource finishCleanup,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        finally
        {
            cleanupEntered.TrySetResult();
            await finishCleanup.Task;
        }

        yield break;
    }

    private sealed class TestMessage;

    private sealed class NoOpClientStreamWriter : IClientStreamWriter<TestMessage>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task CompleteAsync() => Task.CompletedTask;

        public Task WriteAsync(TestMessage message) => Task.CompletedTask;
    }

    private sealed class BlockingClientStreamWriter : IClientStreamWriter<TestMessage>
    {
        public TaskCompletionSource Entered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Finish { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public WriteOptions? WriteOptions { get; set; }

        public Task CompleteAsync() => Task.CompletedTask;

        public async Task WriteAsync(TestMessage message)
        {
            Entered.TrySetResult();
            await Finish.Task;
        }
    }

    private sealed class BlockingStreamReader : IAsyncStreamReader<TestMessage>
    {
        public TaskCompletionSource Entered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CleanupEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FinishCleanup { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TestMessage Current { get; } = new();

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return false;
            }
            finally
            {
                CleanupEntered.TrySetResult();
                await FinishCleanup.Task;
            }
        }
    }
}
