using System.Reflection;
using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Testing;

internal sealed class TestApplicationContext : IApplicationContext
{
    public TestApplicationContext(TestDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        ApplicationId = "AtomUI.City.Testing.TestHost";
        ApplicationInstanceId = Guid.NewGuid();
        ApplicationName = "AtomUI.City.TestHost";
        ApplicationVersion = ResolveApplicationVersion();
        EnvironmentName = "Testing";
        ContentRootPath = Path.GetFullPath(directory.RootPath);
        AppDataPath = Path.GetFullPath(Path.Combine(directory.RootPath, "app-data"));
        StartupArguments = Array.AsReadOnly(Array.Empty<string>());
    }

    public string ApplicationId { get; }

    public Guid ApplicationInstanceId { get; }

    public string ApplicationName { get; }

    public string ApplicationVersion { get; }

    public string EnvironmentName { get; }

    public string ContentRootPath { get; }

    public string AppDataPath { get; }

    public IReadOnlyList<string> StartupArguments { get; }

    private static string ResolveApplicationVersion()
    {
        var assembly = typeof(TestApplicationContext).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return !string.IsNullOrWhiteSpace(informationalVersion)
            ? informationalVersion
            : assembly.GetName().Version?.ToString()
                ?? throw new InvalidOperationException(
                    "AtomUI.City.Testing assembly version is unavailable.");
    }
}
