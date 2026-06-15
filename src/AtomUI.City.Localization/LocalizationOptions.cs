using System.Globalization;

namespace AtomUI.City.Localization;

public sealed class LocalizationOptions
{
    private CultureInfo _defaultCulture = CultureInfo.InvariantCulture;
    private CultureInfo _defaultUICulture = CultureInfo.InvariantCulture;

    public IList<LanguagePackageDescriptor> LanguagePackages { get; } =
        new List<LanguagePackageDescriptor>();

    public CultureInfo DefaultCulture
    {
        get => _defaultCulture;
        set => _defaultCulture = value ?? throw new ArgumentNullException(nameof(value));
    }

    public CultureInfo DefaultUICulture
    {
        get => _defaultUICulture;
        set => _defaultUICulture = value ?? throw new ArgumentNullException(nameof(value));
    }

    public IList<CultureInfo> FallbackCultures { get; } =
        new List<CultureInfo>();
}
