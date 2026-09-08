using System.Globalization;
using System.Collections.ObjectModel;
using System.Runtime.Loader;

namespace AtomUI.City.Localization;

public sealed class LanguagePackageDescriptor
{
    private CultureInfo? _fallbackCulture;
    private IReadOnlyDictionary<string, string>? _inMemoryResources;
    private IReadOnlyList<string> _criticalResourceKeys = Array.Empty<string>();

    public LanguagePackageDescriptor(
        string packageId,
        CultureInfo culture,
        ResourceScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown localization resource scope.");
        }

        PackageId = packageId;
        Culture = CultureInfoSnapshot.Create(culture);
        Scope = scope;
    }

    public string PackageId { get; }

    public CultureInfo Culture { get; }

    public ResourceScope Scope { get; }

    public string? ScopeId { get; init; }

    public LanguagePackageProviderKind ProviderKind { get; init; } =
        LanguagePackageProviderKind.InMemory;

    public CultureInfo? FallbackCulture
    {
        get => _fallbackCulture;
        init => _fallbackCulture = value is null ? null : CultureInfoSnapshot.Create(value);
    }

    public string? Location { get; init; }

    public string? AllowedRootPath { get; init; }

    public string? ResourceBaseName { get; init; }

    public string? Version { get; init; }

    public string? Checksum { get; init; }

    public string? ContributionId { get; init; }

    public AssemblyLoadContext? LoadContext { get; init; }

    public IReadOnlyDictionary<string, string>? InMemoryResources
    {
        get => _inMemoryResources;
        init
        {
            if (value is null)
            {
                _inMemoryResources = null;
                return;
            }

            if (value.Any(resource => string.IsNullOrWhiteSpace(resource.Key) || resource.Value is null))
            {
                throw new ArgumentException(
                    "In-memory resources require non-empty keys and non-null values.",
                    nameof(value));
            }

            _inMemoryResources = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(value, StringComparer.Ordinal));
        }
    }

    public IReadOnlyList<string> CriticalResourceKeys
    {
        get => _criticalResourceKeys;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("Critical resource keys cannot contain empty values.", nameof(value));
            }

            _criticalResourceKeys = Array.AsReadOnly(
                value.Distinct(StringComparer.Ordinal).ToArray());
        }
    }
}
