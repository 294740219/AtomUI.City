namespace AtomUI.City.Data;

public sealed class DataResult<T>
{
    private DataResult(
        DataResultStatus status,
        T? value,
        DataError? error)
    {
        Status = status;
        Value = value;
        Error = error;
    }

    public DataResultStatus Status { get; }

    public T? Value { get; }

    public DataError? Error { get; }

    public bool Succeeded => Status == DataResultStatus.Success;

    public static DataResult<T> Success(T value)
    {
        return new DataResult<T>(DataResultStatus.Success, value, error: null);
    }

    public static DataResult<T> Failed(DataError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new DataResult<T>(DataResultStatus.Failed, value: default, error);
    }

    public static DataResult<T> Partial(T value, DataError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new DataResult<T>(DataResultStatus.Partial, value, error);
    }

    public static DataResult<T> Cancelled(string? message = null)
    {
        return new DataResult<T>(
            DataResultStatus.Cancelled,
            value: default,
            new DataError(DataErrorKind.Cancelled, message ?? "Data operation was cancelled."));
    }

    public static DataResult<T> StaleSuppressed(string? message = null)
    {
        return new DataResult<T>(
            DataResultStatus.StaleSuppressed,
            value: default,
            new DataError(DataErrorKind.Cancelled, message ?? "Data operation result was suppressed."));
    }

    public DataResult<TResponse> Cast<TResponse>()
    {
        if (Succeeded)
        {
            return TryCastValue(Value, out TResponse? response)
                ? DataResult<TResponse>.Success(response!)
                : DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.SerializationError,
                        $"Data result value cannot be cast to '{typeof(TResponse).FullName}'."));
        }

        if (Status == DataResultStatus.Partial)
        {
            return TryCastValue(Value, out TResponse? response)
                ? DataResult<TResponse>.Partial(response!, Error!)
                : DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.SerializationError,
                        $"Partial data result value cannot be cast to '{typeof(TResponse).FullName}'."));
        }

        return Status switch
        {
            DataResultStatus.Cancelled => DataResult<TResponse>.Cancelled(Error?.Message),
            DataResultStatus.StaleSuppressed => DataResult<TResponse>.StaleSuppressed(Error?.Message),
            _ => DataResult<TResponse>.Failed(Error ?? new DataError(DataErrorKind.Unknown, "Data operation failed.")),
        };
    }

    private static bool TryCastValue<TResponse>(T? value, out TResponse? response)
    {
        if (value is TResponse typedValue)
        {
            response = typedValue;
            return true;
        }

        if (value is null && default(TResponse) is null)
        {
            response = default;
            return true;
        }

        response = default;
        return false;
    }
}
