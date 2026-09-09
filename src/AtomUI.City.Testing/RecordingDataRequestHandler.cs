using System.Collections.Concurrent;
using AtomUI.City.Data;

namespace AtomUI.City.Testing;

public sealed record RecordedDataRequest(
    object Request,
    DataRequestContext Context,
    bool IsEntering);

public sealed class RecordingDataRequestHandler : IDataRequestHandler
{
    private readonly ConcurrentQueue<RecordedDataRequest> _records = new();
    private readonly Func<object, DataRequestContext, CancellationToken, ValueTask>? _before;

    public RecordingDataRequestHandler(
        int order = 0,
        Func<object, DataRequestContext, CancellationToken, ValueTask>? before = null)
    {
        Order = order;
        _before = before;
    }

    public int Order { get; }

    public IReadOnlyList<RecordedDataRequest> Records => _records.ToArray();

    public async ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataRequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        _records.Enqueue(new RecordedDataRequest(request, context, IsEntering: true));
        if (_before is not null)
        {
            await _before(request, context, cancellationToken).ConfigureAwait(false);
        }

        var result = await next(cancellationToken).ConfigureAwait(false);
        _records.Enqueue(new RecordedDataRequest(request, context, IsEntering: false));
        return result;
    }
}
