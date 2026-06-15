using AtomUI.City.Data;

namespace AtomUI.City.Data.Tests;

public sealed class SignalRDataTransportTests
{
    [Fact]
    public async Task SignalRTransportInvokesHubMethod()
    {
        SignalRInvocationContext? invocationContext = null;
        CancellationToken observedToken = default;
        var transport = new SignalRDataTransport();
        var request = new SignalRDataRequest<string>(
            "notifications",
            "load-count",
            "NotificationHub",
            "GetUnreadCount",
            (context, token) =>
            {
                invocationContext = context;
                observedToken = token;

                return ValueTask.FromResult("42");
            });
        var cancellation = new CancellationTokenSource();
        var context = DataRequestContext.Create(request, cancellation.Token);
        context.SetCredential(DataCredential.Bearer("hub-token"));

        var result = await transport.SendAsync(
            request,
            context,
            cancellation.Token);

        Assert.True(result.Succeeded);
        Assert.Equal("42", result.Value);
        Assert.Same(context, invocationContext?.Request);
        Assert.Equal("NotificationHub", invocationContext?.HubName);
        Assert.Equal("GetUnreadCount", invocationContext?.MethodName);
        Assert.Equal("hub-token", invocationContext?.Credential?.Parameter);
        Assert.Equal(cancellation.Token, observedToken);
    }

    [Fact]
    public async Task SignalRTransportMapsInvokeFailure()
    {
        var transport = new SignalRDataTransport();
        var request = new SignalRDataRequest<string>(
            "notifications",
            "load-count",
            "NotificationHub",
            "GetUnreadCount",
            (_, _) => throw new InvalidOperationException("hub failed"));

        var result = await transport.SendAsync(
            request,
            DataRequestContext.Create(request, CancellationToken.None));

        Assert.False(result.Succeeded);
        Assert.Equal(DataErrorKind.TransportError, result.Error?.Kind);
    }

    [Fact]
    public async Task SignalRTransportMapsInvokeTimeout()
    {
        var timeoutException = new TaskCanceledException("hub invoke timed out");
        var transport = new SignalRDataTransport();
        var request = new SignalRDataRequest<string>(
            "notifications",
            "load-count",
            "NotificationHub",
            "GetUnreadCount",
            (_, _) => throw timeoutException);

        var result = await transport.SendAsync(
            request,
            DataRequestContext.Create(request, CancellationToken.None),
            CancellationToken.None);

        Assert.Equal(DataResultStatus.Failed, result.Status);
        Assert.Equal(DataErrorKind.Timeout, result.Error?.Kind);
        Assert.Same(timeoutException, result.Error?.Exception);
    }

    [Theory]
    [InlineData("AtomUI.City.Data.SignalRConnectionClosedException", DataErrorKind.ConnectionClosed)]
    [InlineData("AtomUI.City.Data.SignalRReconnectFailedException", DataErrorKind.ReconnectFailed)]
    public async Task SignalRTransportMapsConnectionLifecycleFailures(
        string exceptionTypeName,
        DataErrorKind expectedError)
    {
        var exceptionType = typeof(SignalRDataTransport).Assembly.GetType(exceptionTypeName);
        Assert.NotNull(exceptionType);
        var exception = Assert.IsAssignableFrom<Exception>(
            Activator.CreateInstance(exceptionType, "connection lifecycle failed"));
        var transport = new SignalRDataTransport();
        var request = new SignalRDataRequest<string>(
            "notifications",
            "load-count",
            "NotificationHub",
            "GetUnreadCount",
            (_, _) => throw exception);

        var result = await transport.SendAsync(
            request,
            DataRequestContext.Create(request, CancellationToken.None));

        Assert.False(result.Succeeded);
        Assert.Equal(expectedError, result.Error?.Kind);
        Assert.Same(exception, result.Error?.Exception);
    }
}
