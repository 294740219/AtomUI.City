using System.Collections.Concurrent;
using AtomUI.City.Localization;

namespace AtomUI.City.Fixtures.StressCli.Localization;

public sealed class StressLanguagePackageProvider : ILanguagePackageProvider
{
    private readonly ConcurrentDictionary<(string Culture, string PackageId), int> _loadCounts = [];
    private readonly ConcurrentDictionary<string, byte> _failNext = new(StringComparer.Ordinal);
    private int _delayMilliseconds;

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

    public int GetLoadCount(string cultureName, string packageId)
    {
        return _loadCounts.TryGetValue((cultureName, packageId), out var count) ? count : 0;
    }

    public void FailNext(string packageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        _failNext[packageId] = 0;
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

        try
        {
            var delay = DelayMilliseconds;
            if (delay > 0)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
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
    }

    private static LanguagePackageLoadResult Failed(LocalizationErrorKind kind, string message)
    {
        return LanguagePackageLoadResult.Failed(new LocalizationError(kind, message));
    }
}

public sealed class StressPresentationLocalizationBridge : IPresentationLocalizationBridge
{
    private readonly ConcurrentQueue<CultureState> _appliedStates = [];
    private readonly object _cancellationGate = new();
    private CancellationTokenSource? _cancelCallerOnNextApply;
    private int _failNext;

    public int ApplyCount => _appliedStates.Count;

    public IReadOnlyList<CultureState> AppliedStates => _appliedStates.ToArray();

    public void FailNext() => Interlocked.Exchange(ref _failNext, 1);

    public void CancelCallerOnNextApply(CancellationTokenSource cancellation)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        lock (_cancellationGate)
        {
            _cancelCallerOnNextApply = cancellation;
        }
    }

    public ValueTask<LocalizationResult> ApplyCultureAsync(
        CultureState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        _appliedStates.Enqueue(state);

        CancellationTokenSource? callerCancellation;
        lock (_cancellationGate)
        {
            callerCancellation = _cancelCallerOnNextApply;
            _cancelCallerOnNextApply = null;
        }

        callerCancellation?.Cancel();
        if (Interlocked.Exchange(ref _failNext, 0) != 0)
        {
            return ValueTask.FromResult(
                LocalizationResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PresentationApplyFailed,
                        "Injected Presentation localization bridge failure.")));
        }

        return ValueTask.FromResult(LocalizationResult.Success());
    }
}
