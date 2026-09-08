namespace AtomUI.City.Localization;

internal sealed class LocalizationScopeLease : ILocalizationScopeLease
{
    private LocalizationService? _owner;

    public LocalizationScopeLease(
        LocalizationService owner,
        LocalizationLookupContext context)
    {
        _owner = owner;
        Context = context;
    }

    public LocalizationLookupContext Context { get; }

    public bool IsDisposed => Volatile.Read(ref _owner) is null;

    public void Dispose()
    {
        Interlocked.Exchange(ref _owner, null)?.DeactivateScope(Context);
    }
}
