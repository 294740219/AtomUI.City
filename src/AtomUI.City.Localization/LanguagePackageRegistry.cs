namespace AtomUI.City.Localization;

public sealed class LanguagePackageRegistry
{
    private readonly Dictionary<string, LanguagePackageRegistration> _registrations =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _revokedOwners =
        new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();

    public IReadOnlyList<LanguagePackageRegistration> Registrations
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_registrations.Values.ToArray());
            }
        }
    }

    public IReadOnlyList<LanguagePackageDescriptor> Descriptors
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_registrations.Values
                    .Select(registration => registration.Descriptor)
                    .ToArray());
            }
        }
    }

    public LocalizationResult Register(
        LanguagePackageDescriptor descriptor,
        string ownerId)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        lock (_syncRoot)
        {
            if (_revokedOwners.Contains(ownerId))
            {
                return LocalizationResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.OwnerRevoked,
                        $"Language package owner '{ownerId}' has been revoked."));
            }

            if (_registrations.ContainsKey(descriptor.PackageId))
            {
                return LocalizationResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PackageAlreadyRegistered,
                        $"Language package '{descriptor.PackageId}' is already registered."));
            }

            _registrations.Add(
                descriptor.PackageId,
                new LanguagePackageRegistration(descriptor, ownerId));

            return LocalizationResult.Success();
        }
    }

    public int RevokeOwner(string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        lock (_syncRoot)
        {
            _revokedOwners.Add(ownerId);
            var revokedPackageIds = _registrations
                .Where(pair => string.Equals(pair.Value.OwnerId, ownerId, StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var packageId in revokedPackageIds)
            {
                _registrations.Remove(packageId);
            }

            return revokedPackageIds.Length;
        }
    }
}
