namespace AtomUI.City.Routing;

public sealed class RouteContributionLease : IDisposable, IAsyncDisposable
{
    private readonly Action<string> _release;
    private int _disposed;

    internal RouteContributionLease(string contributionId, Action<string> release)
    {
        ContributionId = contributionId;
        _release = release;
    }

    public string ContributionId { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                _release(ContributionId);
            }
            catch
            {
                Volatile.Write(ref _disposed, 0);
                throw;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
