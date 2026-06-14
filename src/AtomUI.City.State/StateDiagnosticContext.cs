using System.Globalization;

namespace AtomUI.City.State;

internal static class StateDiagnosticContext
{
    public static IReadOnlyDictionary<string, string?> Create(
        params (string Key, string? Value)[] entries)
    {
        var context = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (key, value) in entries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            context[key] = value;
        }

        return context;
    }

    public static string TypeName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.FullName ?? type.Name;
    }

    public static string Version(long version)
    {
        return version.ToString(CultureInfo.InvariantCulture);
    }
}
