namespace AtomUI.City.Data;

public sealed class DataConcurrencyOptions
{
    private string? _operationKey;
    private string? _resourceKey;
    private int _maximumQueueLength = 256;
    private DataConcurrencyPolicy _policy;

    public DataConcurrencyPolicy Policy
    {
        get => _policy;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(Policy), value, "Data concurrency policy is not supported.");
            }

            _policy = value;
        }
    }

    public string? OperationKey
    {
        get => _operationKey;
        init => _operationKey = ValidateOptional(value, nameof(OperationKey));
    }

    public string? ResourceKey
    {
        get => _resourceKey;
        init => _resourceKey = ValidateOptional(value, nameof(ResourceKey));
    }

    public int MaximumQueueLength
    {
        get => _maximumQueueLength;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, nameof(MaximumQueueLength));
            _maximumQueueLength = value;
        }
    }

    public static DataConcurrencyOptions AllowConcurrent { get; } = new();

    private static string? ValidateOptional(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }

        return value;
    }
}

public delegate ValueTask<DataResult<TResponse>> DataOperationDelegate<TResponse>(
    CancellationToken cancellationToken);

public interface IDataOperationScheduler
{
    ValueTask<DataResult<TResponse>> ExecuteAsync<TResponse>(
        DataRequest<TResponse> request,
        DataOperationDelegate<TResponse> operation,
        CancellationToken cancellationToken = default);
}
