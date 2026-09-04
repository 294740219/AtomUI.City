namespace AtomUI.City.EventBus;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GeneratedEventManifestAttribute : Attribute
{
    public const int CurrentVersion = 1;

    public GeneratedEventManifestAttribute(Type registrarType, int version)
    {
        RegistrarType = registrarType ?? throw new ArgumentNullException(nameof(registrarType));
        Version = version == CurrentVersion
            ? version
            : throw new ArgumentOutOfRangeException(nameof(version), version, $"Generated event manifest version must be {CurrentVersion}.");
    }

    public Type RegistrarType { get; }

    public int Version { get; }
}
