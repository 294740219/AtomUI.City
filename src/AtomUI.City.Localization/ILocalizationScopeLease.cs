namespace AtomUI.City.Localization;

public interface ILocalizationScopeLease : IDisposable
{
    LocalizationLookupContext Context { get; }

    bool IsDisposed { get; }
}
