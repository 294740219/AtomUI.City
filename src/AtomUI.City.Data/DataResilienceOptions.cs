namespace AtomUI.City.Data;

public sealed class DataResilienceOptions
{
    private TimeSpan? _timeout;
    private int _maxRetryAttempts;
    private TimeSpan _retryDelay;
    private string? _policyName;
    private DataCircuitBreakerOptions _circuitBreaker = DataCircuitBreakerOptions.Disabled;
    private DataRateLimitOptions _rateLimit = DataRateLimitOptions.Disabled;
    private DataResiliencePolicyScope _scope;

    public TimeSpan? Timeout
    {
        get => _timeout;
        init
        {
            if (value.HasValue && value.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(Timeout), value, "Timeout must be greater than zero.");
            }

            _timeout = value;
        }
    }

    public int MaxRetryAttempts
    {
        get => _maxRetryAttempts;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(MaxRetryAttempts));
            _maxRetryAttempts = value;
        }
    }

    public bool AllowMutationRetry { get; init; }

    public TimeSpan RetryDelay
    {
        get => _retryDelay;
        init
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(RetryDelay), value, "Retry delay cannot be negative.");
            }

            _retryDelay = value;
        }
    }

    public string? PolicyName
    {
        get => _policyName;
        init
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(PolicyName));
            }

            _policyName = value;
        }
    }

    public DataResiliencePolicyScope Scope
    {
        get => _scope;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(Scope), value, "Data resilience policy scope is not supported.");
            }

            _scope = value;
        }
    }

    public DataCircuitBreakerOptions CircuitBreaker
    {
        get => _circuitBreaker;
        init => _circuitBreaker = value ?? throw new ArgumentNullException(nameof(CircuitBreaker));
    }

    public DataRateLimitOptions RateLimit
    {
        get => _rateLimit;
        init => _rateLimit = value ?? throw new ArgumentNullException(nameof(RateLimit));
    }

    public bool EnableFallback { get; init; }

    public static DataResilienceOptions None { get; } = new();
}
