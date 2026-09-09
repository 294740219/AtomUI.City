using AtomUI.City.Data;

namespace AtomUI.City.Testing.Tests;

public sealed class DataTestDoublesTests
{
    [Fact]
    public async Task ScriptedTransportReturnsResponsesInOrderAndRecordsInvocations()
    {
        var transport = new ScriptedDataTransport();
        transport.Enqueue(ScriptedDataResponse.Success("first"));
        transport.Enqueue(ScriptedDataResponse.Failed(
            new DataError(DataErrorKind.ServiceUnavailable, "offline")));
        using var pipeline = new DataRequestPipeline(transport);

        var first = await pipeline.SendAsync(Request());
        var second = await pipeline.SendAsync(Request());

        Assert.Equal("first", first.Value);
        Assert.Equal(DataErrorKind.ServiceUnavailable, second.Error?.Kind);
        Assert.Equal(2, transport.Invocations.Count);
    }

    [Fact]
    public async Task ScriptedCredentialAndRecordingHandlerExposePipelineOrder()
    {
        var credential = new ScriptedDataCredentialProvider();
        credential.Enqueue(DataCredentialResult.Success(new DataCredential("Bearer", "token")));
        var transport = new ScriptedDataTransport();
        transport.Enqueue(ScriptedDataResponse.Success("ok"));
        var handler = new RecordingDataRequestHandler();
        using var pipeline = new DataRequestPipeline(
            transport,
            credentialProvider: credential,
            handlers: [handler]);
        var request = new DataRequest<string>("testing", "secure", DataTransportKind.Http)
        {
            Authentication = DataAuthenticationOptions.Bearer(),
        };

        var result = await pipeline.SendAsync(request);

        Assert.Equal("ok", result.Value);
        Assert.Single(credential.Requests);
        Assert.Equal([true, false], handler.Records.Select(static record => record.IsEntering));
        Assert.Equal("Bearer", handler.Records[0].Context.Credential?.Scheme);
    }

    [Fact]
    public async Task FakeConnectionTracksLifecycleAndIsIdempotentOnStop()
    {
        var connection = new FakeDataConnection(
            "testing-connection",
            new DataConnectionOwner(DataConnectionOwnerKind.Manual, "test"));

        await connection.StartAsync();
        await connection.StopAsync();
        await connection.StopAsync();

        Assert.Equal(1, connection.StartCount);
        Assert.Equal(1, connection.StopCount);
        Assert.Equal(DataConnectionState.Stopped, connection.State);
    }

    private static DataRequest<string> Request() =>
        new("testing", "scripted", DataTransportKind.Http);
}
