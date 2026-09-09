using AtomUI.City.Core.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace AtomUI.City.Security;

internal static class SecurityDiagnostics
{
    public static string? RedactIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }

    public static void Write(
        IHostDiagnostics? diagnostics,
        string code,
        string message,
        HostDiagnosticSeverity severity,
        IReadOnlyDictionary<string, string?>? context = null)
    {
        if (diagnostics is null)
        {
            return;
        }

        try
        {
            diagnostics.Write(new HostDiagnosticRecord(code, message, severity)
            {
                Context = context ?? EmptyContext,
            });
        }
        catch
        {
            // Diagnostics must never change a Security result or state transition.
        }
    }

    private static IReadOnlyDictionary<string, string?> EmptyContext { get; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);
}
