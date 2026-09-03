using System.Runtime.CompilerServices;
using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.Diagnostics;

/// <summary>
/// Represents application host builder diagnostics extensions.
/// </summary>
public static class ApplicationHostBuilderDiagnosticsExtensions
{
    /// <summary>
    /// Executes the get build diagnostics operation.
    /// </summary>
    public static IHostDiagnostics GetBuildDiagnostics(this IApplicationHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return ApplicationHostBuilderDiagnosticsStore.GetOrCreate(builder);
    }
}

internal static class ApplicationHostBuilderDiagnosticsStore
{
    private static readonly ConditionalWeakTable<
        IApplicationHostBuilder,
        InMemoryHostDiagnostics> Stores = new();

    public static IHostDiagnostics GetOrCreate(IApplicationHostBuilder builder)
    {
        return Stores.GetValue(
            builder,
            static _ => new InMemoryHostDiagnostics(1024));
    }
}
