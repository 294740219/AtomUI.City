using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.Diagnostics;

public static class ApplicationHostBuilderDiagnosticsExtensions
{
    public static IHostDiagnostics GetBuildDiagnostics(this IApplicationHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return ApplicationHostBuilderDiagnosticsStore.GetOrCreate(builder);
    }
}

internal static class ApplicationHostBuilderDiagnosticsStore
{
    private const string Key = "AtomUI.City.Core.Diagnostics.BuildCollector";

    public static IHostDiagnostics GetOrCreate(IApplicationHostBuilder builder)
    {
        if (builder.Properties.TryGetValue(Key, out var value) && value is IHostDiagnostics diagnostics)
        {
            return diagnostics;
        }

        diagnostics = new InMemoryHostDiagnostics(1024);
        builder.Properties[Key] = diagnostics;

        return diagnostics;
    }
}
