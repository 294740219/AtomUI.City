namespace AtomUI.City.Localization;

public sealed class FileLanguagePackageProvider : ILanguagePackageProvider
{
    public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.File;

    public async ValueTask<LanguagePackageLoadResult> LoadAsync(
        LanguagePackageDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(descriptor.Location) || !File.Exists(descriptor.Location))
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PackageNotFound,
                    $"File language package '{descriptor.Location}' was not found."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = File.OpenRead(descriptor.Location);
            cancellationToken.ThrowIfCancellationRequested();

            var result = LocPackReader.Read(stream, descriptor);
            cancellationToken.ThrowIfCancellationRequested();

            return result;
        }
        catch (OperationCanceledException exception)
        {
            return Cancelled(cancellationToken, exception);
        }
        catch (Exception exception)
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PackageLoadFailed,
                    exception.Message,
                    Exception: exception));
        }
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
