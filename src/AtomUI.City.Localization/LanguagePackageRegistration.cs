namespace AtomUI.City.Localization;

public sealed class LanguagePackageRegistration
{
    public LanguagePackageRegistration(
        LanguagePackageDescriptor descriptor,
        string ownerId)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        Descriptor = descriptor;
        OwnerId = ownerId;
    }

    public LanguagePackageDescriptor Descriptor { get; }

    public string OwnerId { get; }
}
