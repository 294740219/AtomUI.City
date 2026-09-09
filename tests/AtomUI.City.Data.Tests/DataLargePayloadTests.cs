using System.Net;
using System.Net.Http.Headers;

namespace AtomUI.City.Data.Tests;

public sealed class DataLargePayloadTests
{
    [Fact]
    public async Task UploadStreamsContentAndReportsProgress()
    {
        byte[]? uploaded = null;
        using var httpClient = new HttpClient(new DelegateHandler(async (request, token) =>
        {
            uploaded = await request.Content!.ReadAsByteArrayAsync(token);
            return new HttpResponseMessage(HttpStatusCode.Created);
        }));
        var client = new DataLargePayloadClient(httpClient);
        var payload = Enumerable.Range(0, 2 * 1024 * 1024).Select(static index => (byte)index).ToArray();
        var progress = new List<DataTransferProgress>();
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://data.test/upload");

        var result = await client.UploadAsync(
            request,
            new MemoryStream(payload, writable: false),
            payload.Length,
            (value, _) =>
            {
                progress.Add(value);
                return ValueTask.CompletedTask;
            },
            new DataTransferOptions { BufferSize = 4096, ProgressInterval = TimeSpan.Zero });

        Assert.True(result.Succeeded);
        Assert.Equal(payload, uploaded);
        Assert.Equal(payload.Length, result.Value?.BytesTransferred);
        Assert.Equal(DataTransferStage.Starting, progress[0].Stage);
        Assert.Equal(DataTransferStage.Completed, progress[^1].Stage);
    }

    [Fact]
    public async Task DownloadResumeUsesRangeAndAppendsAtRequestedOffset()
    {
        RangeHeaderValue? observedRange = null;
        using var httpClient = new HttpClient(new DelegateHandler((request, _) =>
        {
            observedRange = request.Headers.Range;
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([4, 5, 6]),
            };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(3, 5, 6);
            return Task.FromResult(response);
        }));
        var client = new DataLargePayloadClient(httpClient);
        await using var destination = new MemoryStream([1, 2, 3, 0, 0, 0], writable: true);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://data.test/file");

        var result = await client.DownloadAsync(
            request,
            destination,
            options: new DataTransferOptions { ResumeOffset = 3 });

        Assert.True(result.Succeeded);
        Assert.Equal(3, observedRange?.Ranges.Single().From);
        Assert.Equal([1, 2, 3, 4, 5, 6], destination.ToArray());
        Assert.Equal(6, result.Value?.BytesTransferred);
    }

    [Fact]
    public async Task UnsupportedRangeCanRestartSeekableDestination()
    {
        using var httpClient = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([7, 8]),
            })));
        var client = new DataLargePayloadClient(httpClient);
        await using var destination = new MemoryStream([1, 2, 3, 4], writable: true);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://data.test/file");

        var result = await client.DownloadAsync(
            request,
            destination,
            options: new DataTransferOptions
            {
                ResumeOffset = 3,
                RangeUnsupportedPolicy = DataRangeUnsupportedPolicy.Restart,
            });

        Assert.True(result.Succeeded);
        Assert.Equal([7, 8], destination.ToArray());
    }

    [Fact]
    public async Task ResumeRejectsMismatchedContentRangeWithoutMutatingDestination()
    {
        using var httpClient = new HttpClient(new DelegateHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([4, 5, 6]),
            };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(2, 4, 6);
            return Task.FromResult(response);
        }));
        var client = new DataLargePayloadClient(httpClient);
        await using var destination = new MemoryStream([1, 2, 3], writable: true);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://data.test/file");

        var result = await client.DownloadAsync(
            request,
            destination,
            options: new DataTransferOptions { ResumeOffset = 3 });

        Assert.Equal(DataErrorKind.PolicyRejected, result.Error?.Kind);
        Assert.Equal([1, 2, 3], destination.ToArray());
    }

    [Fact]
    public async Task ResumeRejectsDestinationShorterThanOffset()
    {
        using var httpClient = new HttpClient(new DelegateHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([4, 5, 6]),
            };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(3, 5, 6);
            return Task.FromResult(response);
        }));
        var client = new DataLargePayloadClient(httpClient);
        await using var destination = new MemoryStream([1, 2], writable: true);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://data.test/file");

        var result = await client.DownloadAsync(
            request,
            destination,
            options: new DataTransferOptions { ResumeOffset = 3 });

        Assert.Equal(DataErrorKind.PolicyRejected, result.Error?.Kind);
        Assert.Equal([1, 2], destination.ToArray());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task DownloadRejectsPayloadThatDoesNotMatchDeclaredLength(long declaredLength)
    {
        using var httpClient = new HttpClient(new DelegateHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3]),
            };
            response.Content.Headers.ContentLength = declaredLength;
            return Task.FromResult(response);
        }));
        var client = new DataLargePayloadClient(httpClient);
        await using var destination = new MemoryStream();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://data.test/file");

        var result = await client.DownloadAsync(request, destination);

        Assert.Equal(DataErrorKind.StreamProtocolError, result.Error?.Kind);
    }

    [Fact]
    public async Task CancelledTemporaryDownloadDeletesPartialFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"atomui-city-data-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var httpClient = new HttpClient(new DelegateHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }));
            var client = new DataLargePayloadClient(httpClient);
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://data.test/file");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

            var result = await client.DownloadToTemporaryFileAsync(
                request,
                options: new DataTransferOptions { TemporaryDirectory = directory },
                cancellationToken: cancellation.Token);

            Assert.Equal(DataResultStatus.Cancelled, result.Status);
            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ProgressFailureIsDiagnosedWithoutFailingTransfer()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        using var httpClient = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3]),
            })));
        var client = new DataLargePayloadClient(httpClient, diagnostics);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://data.test/file");
        await using var destination = new MemoryStream();

        var result = await client.DownloadAsync(
            request,
            destination,
            (_, _) => throw new InvalidOperationException("observer failed"),
            new DataTransferOptions { ProgressInterval = TimeSpan.Zero });

        Assert.True(result.Succeeded);
        Assert.Contains(diagnostics.Records, record => record.Code == DataDiagnosticIds.TransferProgressFailed);
    }

    [Fact]
    public async Task TemporaryFileLeaseDeletesUncommittedFileAndRejectsLateCommit()
    {
        using var httpClient = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3]),
            })));
        var client = new DataLargePayloadClient(httpClient);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://data.test/file");

        var result = await client.DownloadToTemporaryFileAsync(request);
        var temporaryFile = Assert.IsType<DataTemporaryFile>(result.Value);
        Assert.True(File.Exists(temporaryFile.Path));

        await temporaryFile.DisposeAsync();

        Assert.False(File.Exists(temporaryFile.Path));
        Assert.Throws<ObjectDisposedException>(temporaryFile.Commit);
    }

    [Fact]
    public async Task TemporaryDirectoryFailureReturnsLocalStorageError()
    {
        var path = Path.GetTempFileName();
        try
        {
            using var httpClient = new HttpClient(new DelegateHandler((_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK))));
            var client = new DataLargePayloadClient(httpClient);
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://data.test/file");

            var result = await client.DownloadToTemporaryFileAsync(
                request,
                options: new DataTransferOptions { TemporaryDirectory = path });

            Assert.Equal(DataErrorKind.LocalStorageError, result.Error?.Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}
