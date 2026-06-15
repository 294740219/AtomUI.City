using System.Globalization;
using System.Reflection;

namespace AtomUI.City.Localization;

public sealed class AssemblyLanguagePackageProvider : ILanguagePackageProvider
{
    public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.Assembly;

    public IReadOnlyList<LanguagePackageDescriptor> Discover(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return Array.AsReadOnly(assembly
            .GetCustomAttributes<LanguagePackageAttribute>()
            .Select(attribute => CreateDescriptor(assembly, attribute))
            .ToArray());
    }

    public ValueTask<LanguagePackageLoadResult> LoadAsync(
        LanguagePackageDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(Cancelled(cancellationToken));
        }

        if (string.IsNullOrWhiteSpace(descriptor.Location))
        {
            return ValueTask.FromResult(
                LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PackageNotFound,
                        "Assembly language package location is required.")));
        }

        if (string.IsNullOrWhiteSpace(descriptor.ResourceBaseName))
        {
            return ValueTask.FromResult(
                LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PackageNotFound,
                        "Assembly language package resource name is required.")));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assembly = Assembly.LoadFrom(descriptor.Location);
            cancellationToken.ThrowIfCancellationRequested();
            var resourceName = ResolveResourceName(assembly, descriptor.ResourceBaseName);

            if (resourceName is null)
            {
                return ValueTask.FromResult(
                    LanguagePackageLoadResult.Failed(
                        new LocalizationError(
                            LocalizationErrorKind.PackageNotFound,
                            $"Embedded localization resource '{descriptor.ResourceBaseName}' was not found.")));
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            cancellationToken.ThrowIfCancellationRequested();
            if (stream is null)
            {
                return ValueTask.FromResult(
                    LanguagePackageLoadResult.Failed(
                        new LocalizationError(
                            LocalizationErrorKind.PackageNotFound,
                            $"Embedded localization resource '{resourceName}' was not found.")));
            }

            return ValueTask.FromResult(LocPackReader.Read(stream, descriptor));
        }
        catch (OperationCanceledException exception)
        {
            return ValueTask.FromResult(Cancelled(cancellationToken, exception));
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(
                LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PackageLoadFailed,
                        exception.Message,
                        Exception: exception)));
        }
    }

    private static LanguagePackageDescriptor CreateDescriptor(
        Assembly assembly,
        LanguagePackageAttribute attribute)
    {
        return new LanguagePackageDescriptor(
            attribute.PackageId,
            CultureInfo.GetCultureInfo(attribute.Culture),
            attribute.Scope)
        {
            ProviderKind = LanguagePackageProviderKind.Assembly,
            FallbackCulture = string.IsNullOrWhiteSpace(attribute.FallbackCulture)
                ? null
                : CultureInfo.GetCultureInfo(attribute.FallbackCulture),
            Location = string.IsNullOrWhiteSpace(assembly.Location) ? null : assembly.Location,
            ResourceBaseName = attribute.ResourceBaseName,
            Version = attribute.Version,
            Checksum = attribute.Checksum,
            ContributionId = attribute.ContributionId,
        };
    }

    private static string? ResolveResourceName(Assembly assembly, string resourceBaseName)
    {
        var names = assembly.GetManifestResourceNames();

        return names.FirstOrDefault(name => string.Equals(name, resourceBaseName, StringComparison.Ordinal))
            ?? names.FirstOrDefault(name => name.EndsWith(resourceBaseName, StringComparison.Ordinal));
    }

    private static LanguagePackageLoadResult Cancelled(
        CancellationToken cancellationToken,
        OperationCanceledException? exception = null)
    {
        return LanguagePackageLoadResult.Failed(
            new LocalizationError(
                LocalizationErrorKind.Cancelled,
                "Language package load was cancelled.",
                exception ?? new OperationCanceledException(cancellationToken)));
    }
}
