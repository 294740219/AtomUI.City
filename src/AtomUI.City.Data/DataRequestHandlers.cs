namespace AtomUI.City.Data;

public delegate ValueTask<DataResult<TResponse>> DataRequestHandlerDelegate<TResponse>(
    CancellationToken cancellationToken);

public interface IDataRequestHandler
{
    int Order { get; }

    ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataRequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}

public interface IDataRequestHandlerSource
{
    IReadOnlyList<IDataRequestHandler> GetHandlers<TResponse>(DataRequest<TResponse> request);
}

internal static class DataRequestHandlerPipeline
{
    public static ValueTask<DataResult<TResponse>> InvokeAsync<TResponse>(
        IReadOnlyList<IDataRequestHandler> handlers,
        DataRequest<TResponse> request,
        DataRequestContext context,
        Func<CancellationToken, ValueTask<DataResult<TResponse>>> terminal,
        CancellationToken cancellationToken)
    {
        return InvokeAt(0, cancellationToken);

        ValueTask<DataResult<TResponse>> InvokeAt(
            int index,
            CancellationToken currentCancellationToken)
        {
            currentCancellationToken.ThrowIfCancellationRequested();
            if (index == handlers.Count)
            {
                return terminal(currentCancellationToken);
            }

            var nextInvoked = 0;
            ValueTask<DataResult<TResponse>> Next(CancellationToken nextCancellationToken)
            {
                if (Interlocked.Exchange(ref nextInvoked, 1) != 0)
                {
                    throw new InvalidOperationException("A data request handler can invoke its continuation only once.");
                }

                return InvokeAt(index + 1, nextCancellationToken);
            }

            return handlers[index].InvokeAsync(request, context, Next, currentCancellationToken);
        }
    }
}
