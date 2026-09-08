using System.Globalization;

namespace AtomUI.City.Localization;

public sealed class CultureState
{
    public CultureState(
        CultureInfo currentCulture,
        CultureInfo currentUICulture,
        IReadOnlyList<CultureInfo> fallbackCultures,
        long revision,
        IReadOnlyList<string> loadedPackageIds)
    {
        ArgumentNullException.ThrowIfNull(fallbackCultures);
        ArgumentNullException.ThrowIfNull(loadedPackageIds);
        if (fallbackCultures.Any(culture => culture is null))
        {
            throw new ArgumentException("Fallback cultures cannot contain null values.", nameof(fallbackCultures));
        }

        if (loadedPackageIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Loaded package ids cannot contain empty values.", nameof(loadedPackageIds));
        }

        CurrentCulture = CultureInfoSnapshot.Create(currentCulture);
        CurrentUICulture = CultureInfoSnapshot.Create(currentUICulture);
        FallbackCultures = Array.AsReadOnly(
            fallbackCultures.Select(CultureInfoSnapshot.Create).ToArray());
        Revision = revision;
        LoadedPackageIds = Array.AsReadOnly(loadedPackageIds.ToArray());
    }

    public CultureInfo CurrentCulture { get; }

    public CultureInfo CurrentUICulture { get; }

    public IReadOnlyList<CultureInfo> FallbackCultures { get; }

    public long Revision { get; }

    public IReadOnlyList<string> LoadedPackageIds { get; }
}
