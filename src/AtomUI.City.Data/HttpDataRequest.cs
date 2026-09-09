namespace AtomUI.City.Data;

public sealed class HttpDataRequest<TResponse> : DataRequest<TResponse>
{
    public HttpDataRequest(
        string clientId,
        string operationName,
        string clientName,
        Func<DataRequestContext, HttpRequestMessage> requestFactory,
        Func<HttpResponseMessage, ValueTask<TResponse>> responseMapper,
        DataAccessMode accessMode = DataAccessMode.Query)
        : this(
            clientId,
            operationName,
            clientName,
            requestFactory,
            (response, _) => responseMapper(response),
            accessMode)
    {
        ResponseMapper = responseMapper ?? throw new ArgumentNullException(nameof(responseMapper));
    }

    public HttpDataRequest(
        string clientId,
        string operationName,
        string clientName,
        Func<DataRequestContext, HttpRequestMessage> requestFactory,
        Func<HttpResponseMessage, CancellationToken, ValueTask<TResponse>> responseMapper,
        DataAccessMode accessMode = DataAccessMode.Query)
        : base(clientId, operationName, DataTransportKind.Http, accessMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        ClientName = clientName;
        RequestFactory = requestFactory ?? throw new ArgumentNullException(nameof(requestFactory));
        ResponseMapperWithCancellation = responseMapper ?? throw new ArgumentNullException(nameof(responseMapper));
        ResponseMapper = response => ResponseMapperWithCancellation(response, CancellationToken.None);
    }

    public string ClientName { get; }

    public Func<DataRequestContext, HttpRequestMessage> RequestFactory { get; }

    public Func<HttpResponseMessage, ValueTask<TResponse>> ResponseMapper { get; }

    public Func<HttpResponseMessage, CancellationToken, ValueTask<TResponse>> ResponseMapperWithCancellation { get; }
}
