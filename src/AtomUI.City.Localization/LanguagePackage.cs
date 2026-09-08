namespace AtomUI.City.Localization;

public sealed class LanguagePackage : IDisposable
{
    private readonly IReadOnlyDictionary<string, string> _strings;
    private int _disposed;

    private LanguagePackage(
        LanguagePackageDescriptor descriptor,
        IReadOnlyDictionary<string, string> strings)
    {
        Descriptor = descriptor;
        _strings = new Dictionary<string, string>(strings, StringComparer.Ordinal);
    }

    public LanguagePackageDescriptor Descriptor { get; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public static LanguagePackage Create(
        LanguagePackageDescriptor descriptor,
        IReadOnlyDictionary<string, string> strings)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(strings);
        if (strings.Any(resource => string.IsNullOrWhiteSpace(resource.Key) || resource.Value is null))
        {
            throw new ArgumentException(
                "Language package resources require non-empty keys and non-null values.",
                nameof(strings));
        }

        return new LanguagePackage(descriptor, strings);
    }

    public bool TryGetString(string key, out string value)
    {
        if (IsDisposed)
        {
            value = string.Empty;

            return false;
        }

        return _strings.TryGetValue(key, out value!);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
    }
}
