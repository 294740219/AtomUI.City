using AtomUI.City.Data;

namespace AtomUI.City.Data.Tests;

public sealed class GrpcDataTransportTests
{
    [Fact]
    public async Task GrpcTransportExecutesUnaryInvoker()
    {
        var transport = new GrpcDataTransport();
        var request = new GrpcDataRequest<string>(
            "catalog",
            "get-items",
            (_, _) => ValueTask.FromResult(GrpcCallResult<string>.Success("grpc-response")));

        var result = await transport.SendAsync(
            request,
            DataRequestContext.Create(request, CancellationToken.None));

        Assert.True(result.Succeeded);
        Assert.Equal("grpc-response", result.Value);
    }

    [Theory]
    [InlineData(GrpcStatusCode.Unauthenticated, DataErrorKind.AuthenticationRequired)]
    [InlineData(GrpcStatusCode.PermissionDenied, DataErrorKind.AuthorizationForbidden)]
    [InlineData(GrpcStatusCode.DeadlineExceeded, DataErrorKind.DeadlineExceeded)]
    [InlineData(GrpcStatusCode.Unavailable, DataErrorKind.ServiceUnavailable)]
    [InlineData(GrpcStatusCode.NotFound, DataErrorKind.NotFound)]
    public async Task GrpcTransportMapsStatusCodes(GrpcStatusCode statusCode, DataErrorKind expectedError)
    {
        var transport = new GrpcDataTransport();
        var request = new GrpcDataRequest<string>(
            "catalog",
            "get-items",
            (_, _) => ValueTask.FromResult(GrpcCallResult<string>.Failed(statusCode, "grpc failed")));

        var result = await transport.SendAsync(
            request,
            DataRequestContext.Create(request, CancellationToken.None));

        Assert.False(result.Succeeded);
        Assert.Equal(expectedError, result.Error?.Kind);
    }

    [Theory]
    [InlineData("OK", 0)]
    [InlineData("Cancelled", 1)]
    [InlineData("Unknown", 2)]
    [InlineData("InvalidArgument", 3)]
    [InlineData("DeadlineExceeded", 4)]
    [InlineData("NotFound", 5)]
    [InlineData("AlreadyExists", 6)]
    [InlineData("PermissionDenied", 7)]
    [InlineData("ResourceExhausted", 8)]
    [InlineData("FailedPrecondition", 9)]
    [InlineData("Aborted", 10)]
    [InlineData("OutOfRange", 11)]
    [InlineData("Unimplemented", 12)]
    [InlineData("Internal", 13)]
    [InlineData("Unavailable", 14)]
    [InlineData("DataLoss", 15)]
    [InlineData("Unauthenticated", 16)]
    public void GrpcStatusCodeValuesMatchGrpcProtocolNumbers(string statusName, int expectedValue)
    {
        Assert.True(Enum.TryParse(statusName, out GrpcStatusCode statusCode));
        Assert.Equal(expectedValue, (int)statusCode);
    }

    [Theory]
    [InlineData("InvalidArgument", DataErrorKind.ValidationFailed)]
    [InlineData("ResourceExhausted", DataErrorKind.PolicyRejected)]
    [InlineData("FailedPrecondition", DataErrorKind.Conflict)]
    [InlineData("Aborted", DataErrorKind.Conflict)]
    [InlineData("OutOfRange", DataErrorKind.ValidationFailed)]
    [InlineData("Unimplemented", DataErrorKind.ServerError)]
    [InlineData("DataLoss", DataErrorKind.ServerError)]
    public async Task GrpcTransportMapsAdditionalStandardStatusCodes(
        string statusName,
        DataErrorKind expectedError)
    {
        Assert.True(Enum.TryParse(statusName, out GrpcStatusCode statusCode));
        var transport = new GrpcDataTransport();
        var request = new GrpcDataRequest<string>(
            "catalog",
            "get-items",
            (_, _) => ValueTask.FromResult(GrpcCallResult<string>.Failed(statusCode, "grpc failed")));

        var result = await transport.SendAsync(
            request,
            DataRequestContext.Create(request, CancellationToken.None));

        Assert.False(result.Succeeded);
        Assert.Equal(expectedError, result.Error?.Kind);
        Assert.Equal(statusCode.ToString(), result.Error?.TransportStatus);
    }

    [Fact]
    public async Task GrpcTransportMapsCancellation()
    {
        var transport = new GrpcDataTransport();
        var request = new GrpcDataRequest<string>(
            "catalog",
            "get-items",
            (_, _) => throw new OperationCanceledException());

        var result = await transport.SendAsync(
            request,
            DataRequestContext.Create(request, CancellationToken.None));

        Assert.Equal(DataResultStatus.Cancelled, result.Status);
        Assert.Equal(DataErrorKind.Cancelled, result.Error?.Kind);
    }

    [Fact]
    public async Task GrpcTransportMapsCancelledStatusToCancelledResult()
    {
        var transport = new GrpcDataTransport();
        var request = new GrpcDataRequest<string>(
            "catalog",
            "get-items",
            (_, _) => ValueTask.FromResult(GrpcCallResult<string>.Failed(GrpcStatusCode.Cancelled, "cancelled")));

        var result = await transport.SendAsync(
            request,
            DataRequestContext.Create(request, CancellationToken.None));

        Assert.Equal(DataResultStatus.Cancelled, result.Status);
        Assert.Equal(DataErrorKind.Cancelled, result.Error?.Kind);
    }

    [Fact]
    public async Task GrpcTransportMapsInvokerFailureToTransportError()
    {
        var transport = new GrpcDataTransport();
        var callException = new InvalidOperationException("channel unavailable");
        var request = new GrpcDataRequest<string>(
            "catalog",
            "get-items",
            (_, _) => throw callException);

        var result = await transport.SendAsync(
            request,
            DataRequestContext.Create(request, CancellationToken.None));

        Assert.False(result.Succeeded);
        Assert.Equal(DataErrorKind.TransportError, result.Error?.Kind);
        Assert.Same(callException, result.Error?.Exception);
    }
}
