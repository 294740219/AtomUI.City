namespace AtomUI.City.Core.Lifecycle;

public sealed class LifecycleContext
{
    private sealed class NullServiceProvider : IServiceProvider
    {
        public static readonly NullServiceProvider Instance = new();

        private NullServiceProvider()
        {
        }

        public object? GetService(Type serviceType)
        {
            return null;
        }
    }

    public LifecycleContext(
        LifecycleStage stage,
        IServiceProvider? services = null,
        CancellationToken cancellationToken = default,
        string? operationId = null)
    {
        if (operationId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operationId, nameof(operationId));
        }

        Stage = stage;
        Services = services ?? NullServiceProvider.Instance;
        CancellationToken = cancellationToken;
        OperationId = operationId ?? Guid.NewGuid().ToString("N");
    }

    public LifecycleStage Stage { get; }

    public IServiceProvider Services { get; }

    public CancellationToken CancellationToken { get; }

    public string OperationId { get; }

    public IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

    public bool IsShortCircuited { get; private set; }

    public void ShortCircuit()
    {
        IsShortCircuited = true;
    }
}
