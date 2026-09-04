using System.Security.Cryptography;
using System.Text;

namespace AtomUI.City.EventBus;

internal static class EventCorrelationIds
{
    public static string ValidateRequired(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return ValidateOptional(value, paramName)!;
    }

    public static string? ValidateOptional(string? value, string paramName)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Event correlation id cannot be empty.", paramName);
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Event correlation id cannot contain surrounding whitespace.", paramName);
        }

        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                throw new ArgumentException("Event correlation id cannot contain control characters.", paramName);
            }
        }

        return value;
    }

    public static string? ToDiagnosticHash(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}
