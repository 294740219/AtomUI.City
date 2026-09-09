namespace AtomUI.City.Data;

internal static class DataTransportRequestValidator
{
    public static DataResult<TResponse>? Validate<TResponse>(
        DataRequest<TResponse> request,
        DataRequestContext context,
        DataTransportKind expectedTransport)
    {
        if (request.TransportKind != expectedTransport)
        {
            return Reject<TResponse>(
                $"The data request transport must be '{expectedTransport}'.");
        }

        if (!context.BelongsTo(request)
            || context.ClientId != request.ClientId
            || context.OperationName != request.OperationName
            || context.TransportKind != request.TransportKind
            || context.AccessMode != request.AccessMode)
        {
            return Reject<TResponse>(
                "The data request context does not belong to the supplied request.");
        }

        return null;
    }

    private static DataResult<TResponse> Reject<TResponse>(string message)
    {
        return DataResult<TResponse>.Failed(
            new DataError(DataErrorKind.PolicyRejected, message));
    }
}
