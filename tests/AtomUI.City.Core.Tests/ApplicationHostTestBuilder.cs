using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.Tests;

internal static class ApplicationHostTestBuilder
{
    public const string ApplicationId = "AtomUI.City.Core.Tests";
    public const string ApplicationName = "AtomUI.City.Core.Tests";

    public static IApplicationHostBuilder Create(string[]? args = null)
    {
        var builder = ApplicationHost.CreateBuilder(args);
        ConfigureIdentity(builder);

        return builder;
    }

    public static void ConfigureIdentity(IApplicationHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureHost(options =>
        {
            options.ApplicationId = ApplicationId;
            options.ApplicationName = ApplicationName;
        });
    }

    public static IApplicationContext CreateContext()
    {
        var contentRootPath = Path.GetFullPath(Directory.GetCurrentDirectory());

        return new TestApplicationContext(
            ApplicationId,
            Guid.NewGuid(),
            ApplicationName,
            "1.0.0-test",
            "Testing",
            contentRootPath,
            Path.Combine(contentRootPath, "app-data"),
            Array.AsReadOnly(Array.Empty<string>()));
    }

    private sealed record TestApplicationContext(
        string ApplicationId,
        Guid ApplicationInstanceId,
        string ApplicationName,
        string ApplicationVersion,
        string EnvironmentName,
        string ContentRootPath,
        string AppDataPath,
        IReadOnlyList<string> StartupArguments) : IApplicationContext;
}
