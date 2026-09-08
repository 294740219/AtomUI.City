using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace AtomUI.City.Localization;

internal static class LocPackReader
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumPackageSizeBytes = 16 * 1024 * 1024;

    public static LanguagePackageLoadResult Read(
        Stream stream,
        LanguagePackageDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(descriptor);

        if (stream.CanSeek && stream.Length - stream.Position > MaximumPackageSizeBytes)
        {
            return PackageTooLarge();
        }

        using var buffer = new MemoryStream();
        var copyBuffer = new byte[81920];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = stream.Read(copyBuffer, 0, copyBuffer.Length);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaximumPackageSizeBytes)
            {
                return PackageTooLarge();
            }

            buffer.Write(copyBuffer, 0, read);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var bytes = buffer.ToArray();

        var checksumResult = ValidateChecksum(bytes, descriptor);
        if (checksumResult is not null)
        {
            return checksumResult;
        }

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Failed(
                LocalizationErrorKind.PackageSchemaMismatch,
                "Localization pack root must be a JSON object.");
        }

        var rootPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!rootPropertyNames.Add(property.Name))
            {
                return Failed(
                    LocalizationErrorKind.PackageSchemaMismatch,
                    $"Localization pack property '{property.Name}' is declared more than once.");
            }
        }

        if (!root.TryGetProperty("schemaVersion", out var schemaElement)
            || schemaElement.ValueKind != JsonValueKind.Number
            || !schemaElement.TryGetInt32(out var schemaVersion))
        {
            return Failed(
                LocalizationErrorKind.PackageSchemaMismatch,
                "Localization pack property 'schemaVersion' is required and must be an integer.");
        }

        if (schemaVersion != CurrentSchemaVersion)
        {
            return Failed(
                LocalizationErrorKind.PackageSchemaMismatch,
                $"Localization pack schema version '{schemaVersion}' is not supported; expected '{CurrentSchemaVersion}'.");
        }

        if (!TryReadRequiredString(root, "packageId", out var packageId)
            || !TryReadRequiredString(root, "culture", out var cultureName))
        {
            return Failed(
                LocalizationErrorKind.PackageSchemaMismatch,
                "Localization pack properties 'packageId' and 'culture' are required and cannot be empty.");
        }

        if (!string.Equals(packageId, descriptor.PackageId, StringComparison.Ordinal))
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PackageIdentityMismatch,
                    $"Language package id '{packageId}' does not match descriptor '{descriptor.PackageId}'."));
        }

        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException exception)
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PackageCultureMismatch,
                    $"Language package culture '{cultureName}' is invalid.",
                    exception));
        }
        if (!string.Equals(culture.Name, descriptor.Culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PackageCultureMismatch,
                    $"Language package culture '{culture.Name}' does not match descriptor '{descriptor.Culture.Name}'."));
        }

        if (!string.IsNullOrWhiteSpace(descriptor.Version))
        {
            if (!TryReadRequiredString(root, "version", out var packageVersion))
            {
                return Failed(
                    LocalizationErrorKind.PackageSchemaMismatch,
                    "Localization pack property 'version' is required when the descriptor declares a version.");
            }

            if (!string.Equals(packageVersion, descriptor.Version, StringComparison.Ordinal))
            {
                return Failed(
                    LocalizationErrorKind.PackageVersionMismatch,
                    $"Language package version '{packageVersion}' does not match descriptor '{descriptor.Version}'.");
            }
        }

        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("resources", out var resourcesElement)
            || resourcesElement.ValueKind != JsonValueKind.Object)
        {
            return Failed(
                LocalizationErrorKind.PackageSchemaMismatch,
                "Localization pack property 'resources' must be a JSON object.");
        }

        foreach (var property in resourcesElement.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name))
            {
                return Failed(
                    LocalizationErrorKind.InvalidResource,
                    "Localized resource keys cannot be empty.");
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                return Failed(
                    LocalizationErrorKind.InvalidResource,
                    $"Localized resource '{property.Name}' must be a string.");
            }

            if (!resources.TryAdd(property.Name, property.Value.GetString()!))
            {
                return Failed(
                    LocalizationErrorKind.InvalidResource,
                    $"Localized resource '{property.Name}' is declared more than once.");
            }
        }

        return LanguagePackageLoadResult.Success(LanguagePackage.Create(descriptor, resources));
    }

    private static LanguagePackageLoadResult? ValidateChecksum(
        byte[] bytes,
        LanguagePackageDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.Checksum))
        {
            return null;
        }

        const string prefix = "sha256:";
        if (!descriptor.Checksum.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Failed(
                LocalizationErrorKind.PackageChecksumMismatch,
                $"Language package checksum '{descriptor.Checksum}' uses an unsupported format.");
        }

        var expected = descriptor.Checksum[prefix.Length..];
        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)
            ? null
            : Failed(
                LocalizationErrorKind.PackageChecksumMismatch,
                $"Language package checksum does not match descriptor '{descriptor.PackageId}'.");
    }

    private static LanguagePackageLoadResult Failed(
        LocalizationErrorKind kind,
        string message)
    {
        return LanguagePackageLoadResult.Failed(new LocalizationError(kind, message));
    }

    private static LanguagePackageLoadResult PackageTooLarge()
    {
        return Failed(
            LocalizationErrorKind.PackageTooLarge,
            $"Localization pack exceeds the {MaximumPackageSizeBytes / (1024 * 1024)} MiB size limit.");
    }

    private static bool TryReadRequiredString(
        JsonElement root,
        string name,
        out string value)
    {
        if (root.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(element.GetString()))
        {
            value = element.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
