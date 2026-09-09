using System.Collections.Concurrent;

namespace AtomUI.City.Data;

public sealed class DataRequestContext
{
    private readonly object _request;

    private DataRequestContext(
        object request,
        Guid operationId,
        string clientId,
        string operationName,
        DataTransportKind transportKind,
        DataAccessMode accessMode,
        CancellationToken cancellationToken)
    {
        _request = request;
        OperationId = operationId;
        CorrelationId = operationId.ToString("D");
        ClientId = clientId;
        OperationName = operationName;
        TransportKind = transportKind;
        AccessMode = accessMode;
        CancellationToken = cancellationToken;
    }

    public Guid OperationId { get; }

    public string CorrelationId { get; }

    public string ClientId { get; }

    public string OperationName { get; }

    public DataTransportKind TransportKind { get; }

    public DataAccessMode AccessMode { get; }

    public int Attempt { get; internal set; }

    public CancellationToken CancellationToken { get; }

    public DataCredential? Credential { get; private set; }

    public IDictionary<string, object?> Items { get; } =
        new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);

    public void SetCredential(DataCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        Credential = credential;
    }

    internal bool BelongsTo<TResponse>(DataRequest<TResponse> request)
    {
        return ReferenceEquals(_request, request);
    }

    public static DataRequestContext Create<TResponse>(
        DataRequest<TResponse> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new DataRequestContext(
            request,
            Guid.NewGuid(),
            request.ClientId,
            request.OperationName,
            request.TransportKind,
            request.AccessMode,
            cancellationToken);
    }
}
