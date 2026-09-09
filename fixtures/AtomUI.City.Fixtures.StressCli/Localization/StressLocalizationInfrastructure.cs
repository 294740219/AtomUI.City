using System.Collections.Concurrent;
using AtomUI.City.Localization;

namespace AtomUI.City.Fixtures.StressCli.Localization;

public sealed class StressLanguagePackageProvider : ILanguagePackageProvider
{
    private readonly ConcurrentDictionary<(string Culture, string PackageId), int> _loadCounts = [];
    private readonly ConcurrentDictionary<string, byte> _failNext = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _throwNext = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<StressLoadGate>> _loadGates =
        new(StringComparer.Ordinal);
    private int _delayMilliseconds;
    private int _activeLoadCount;
    private int _maximumConcurrentLoads;
    private int _completedLoadCount;

    public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.InMemory;

    public int DelayMilliseconds
    {
        get => Volatile.Read(ref _delayMilliseconds);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            Volatile.Write(ref _delayMilliseconds, value);
        }
    }

    public int TotalLoadCount => _loadCounts.Values.Sum();

    public int UniqueLoadCount => _loadCounts.Count;

    public int MaximumLoadCount => _loadCounts.IsEmpty ? 0 : _loadCounts.Values.Max();

    public int ActiveLoadCount => Volatile.Read(ref _activeLoadCount);

    public int MaximumConcurrentLoads => Volatile.Read(ref _maximumConcurrentLoads);

    public int CompletedLoadCount => Volatile.Read(ref _completedLoadCount);

    public int GetLoadCount(string cultureName, string packageId)
    {
        return _loadCounts.TryGetValue((cultureName, packageId), out var count) ? count : 0;
    }

    public void FailNext(string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        _failNext[packageId] = 0;
    }

    public void ThrowNext(string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        _throwNext[packageId] = 0;
    }

    public StressLoadGate BlockNext(string packageId, bool ignoreCancellation = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        var gate = new StressLoadGate(ignoreCancellation);
        _loadGates.GetOrAdd(packageId, static _ => new ConcurrentQueue<StressLoadGate>()).Enqueue(gate);
        return gate;
    }

    public void ResetCounters() => _loadCounts.Clear();

    public async ValueTask<LanguagePackageLoadResult> LoadAsync(
        LanguagePackageDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.ProviderKind != Kind)
        {
            return Failed(LocalizationErrorKind.InvalidDescriptor, "Stress provider received a non-memory descriptor.");
        }

        _loadCounts.AddOrUpdate(
            (descriptor.Culture.Name, descriptor.PackageId),
            1,
            static (_, count) => count + 1);

        var activeLoads = Interlocked.Increment(ref _activeLoadCount);
        UpdateMaximum(ref _maximumConcurrentLoads, activeLoads);

        try
        {
            if (_loadGates.TryGetValue(descriptor.PackageId, out var gates)
                && gates.TryDequeue(out var gate))
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            var delay = DelayMilliseconds;
            if (delay > 0)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (_throwNext.TryRemove(descriptor.PackageId, out _))
            {
                throw new InvalidOperationException($"Injected provider exception for '{descriptor.PackageId}'.");
            }

            if (_failNext.TryRemove(descriptor.PackageId, out _))
            {
                return Failed(
                    LocalizationErrorKind.PackageLoadFailed,
                    $"Injected load failure for '{descriptor.PackageId}'.");
            }

            if (descriptor.InMemoryResources is null)
            {
                return Failed(
                    LocalizationErrorKind.InvalidDescriptor,
                    $"Language package '{descriptor.PackageId}' does not contain in-memory resources.");
            }

            return LanguagePackageLoadResult.Success(
                LanguagePackage.Create(descriptor, descriptor.InMemoryResources));
        }
        catch (OperationCanceledException exception)
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.Cancelled,
                    $"Language package '{descriptor.PackageId}' load was cancelled.",
                    exception));
        }
        finally
        {
            Interlocked.Decrement(ref _activeLoadCount);
            Interlocked.Increment(ref _completedLoadCount);
        }
    }

    private static LanguagePackageLoadResult Failed(LocalizationErrorKind kind, string message)
    {
        return LanguagePackageLoadResult.Failed(new LocalizationError(kind, message));
    }

    private static void UpdateMaximum(ref int target, int candidate)
    {
        var current = Volatile.Read(ref target);
        while (candidate > current)
        {
            var observed = Interlocked.CompareExchange(ref target, candidate, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }
}

public sealed class StressLoadGate
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal StressLoadGate(bool ignoreCancellation)
    {
        IgnoreCancellation = ignoreCancellation;
    }

    public bool IgnoreCancellation { get; }

    public Task Entered => _entered.Task;

    public void Release() => _release.TrySetResult();

    internal async Task WaitAsync(CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        if (IgnoreCancellation)
        {
            await _release.Task.ConfigureAwait(false);
        }
        else
        {
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed class StressPresentationLocalizationBridge : IPresentationLocalizationBridge
{
    private readonly ConcurrentQueue<CultureState> _appliedStates = [];
    private readonly object _cancellationGate = new();
    private CancellationTokenSource? _cancelCallerOnNextApply;
    private StressBridgeGate? _nextGate;
    private Func<ValueTask>? _nextCallback;
    private int _failNext;
    private int _activeApplyCount;
    private int _maximumConcurrentApplies;

    public int ApplyCount => _appliedStates.Count;

    public IReadOnlyList<CultureState> AppliedStates => _appliedStates.ToArray();

    public int ActiveApplyCount => Volatile.Read(ref _activeApplyCount);

    public int MaximumConcurrentApplies => Volatile.Read(ref _maximumConcurrentApplies);

    public void FailNext() => Interlocked.Exchange(ref _failNext, 1);

    public void CancelCallerOnNextApply(CancellationTokenSource cancellation)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        lock (_cancellationGate)
        {
            _cancelCallerOnNextApply = cancellation;
        }
    }

    public StressBridgeGate BlockNextApply()
    {
        lock (_cancellationGate)
        {
            if (_nextGate is not null)
            {
                throw new InvalidOperationException("A bridge gate is already pending.");
            }

            _nextGate = new StressBridgeGate();
            return _nextGate;
        }
    }

    public void InvokeOnNextApply(Func<ValueTask> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_cancellationGate)
        {
            if (_nextCallback is not null)
            {
                throw new InvalidOperationException("A bridge callback is already pending.");
            }

            _nextCallback = callback;
        }
    }

    public async ValueTask<LocalizationResult> ApplyCultureAsync(
        CultureState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        var activeApplies = Interlocked.Increment(ref _activeApplyCount);
        UpdateMaximum(ref _maximumConcurrentApplies, activeApplies);

        CancellationTokenSource? callerCancellation;
        StressBridgeGate? gate;
        Func<ValueTask>? callback;
        lock (_cancellationGate)
        {
            callerCancellation = _cancelCallerOnNextApply;
            _cancelCallerOnNextApply = null;
            gate = _nextGate;
            _nextGate = null;
            callback = _nextCallback;
            _nextCallback = null;
        }

        try
        {
            _appliedStates.Enqueue(state);
            callerCancellation?.Cancel();
            if (gate is not null)
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (callback is not null)
            {
                await callback().ConfigureAwait(false);
            }

            if (Interlocked.Exchange(ref _failNext, 0) != 0)
            {
                return LocalizationResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PresentationApplyFailed,
                        "Injected Presentation localization bridge failure."));
            }

            return LocalizationResult.Success();
        }
        finally
        {
            Interlocked.Decrement(ref _activeApplyCount);
        }
    }

    private static void UpdateMaximum(ref int target, int candidate)
    {
        var current = Volatile.Read(ref target);
        while (candidate > current)
        {
            var observed = Interlocked.CompareExchange(ref target, candidate, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }
}

public sealed class StressBridgeGate
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public void Release() => _release.TrySetResult();

    internal async Task WaitAsync(CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
