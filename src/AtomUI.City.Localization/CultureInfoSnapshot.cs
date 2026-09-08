using System.Globalization;

namespace AtomUI.City.Localization;

internal static class CultureInfoSnapshot
{
    public static CultureInfo Create(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return culture.IsReadOnly
            ? culture
            : CultureInfo.ReadOnly((CultureInfo)culture.Clone());
    }
}
