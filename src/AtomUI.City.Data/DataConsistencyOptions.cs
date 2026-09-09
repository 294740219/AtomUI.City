namespace AtomUI.City.Data;

public sealed class DataConsistencyOptions
{
    private IReadOnlyList<DataCacheInvalidation> _invalidationsOnSuccess = [];

    public IReadOnlyList<DataCacheInvalidation> InvalidationsOnSuccess
    {
        get => _invalidationsOnSuccess;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Any(static invalidation => invalidation is null))
            {
                throw new ArgumentException("Cache invalidations cannot contain null values.", nameof(InvalidationsOnSuccess));
            }

            _invalidationsOnSuccess = Array.AsReadOnly(value.ToArray());
        }
    }

    public IDataOptimisticUpdate? OptimisticUpdate { get; init; }

    public bool RollBackOnCancellation { get; init; } = true;

    public static DataConsistencyOptions None { get; } = new();
}

public interface IDataOptimisticUpdate
{
    ValueTask ApplyAsync(DataRequestContext context, CancellationToken cancellationToken = default);

    ValueTask ConfirmAsync(DataRequestContext context, CancellationToken cancellationToken = default);

    ValueTask RollBackAsync(DataRequestContext context, CancellationToken cancellationToken = default);
}
