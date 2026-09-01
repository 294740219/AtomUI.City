using System.Reflection;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AtomUI.City.Core.Tests;

public sealed class ApplicationHostOptionsTests
{
    [Fact]
    public async Task ConfigureHostRegistersApplicationHostOptions()
    {
        var builder = ApplicationHostTestBuilder.Create();

        builder.ConfigureHost(options =>
        {
            options.ApplicationName = "Sample.Desktop";
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
        });

        await using var host = builder.Build();

        var options = host.Services.GetRequiredService<IOptions<ApplicationHostOptions>>().Value;

        Assert.Equal("Sample.Desktop", options.ApplicationName);
        Assert.Equal(ApplicationHostTestBuilder.ApplicationId, options.ApplicationId);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ShutdownTimeout);
        Assert.Equal("Sample.Desktop", host.Context.ApplicationName);
    }

    [Fact]
    public void ApplicationHostOptionsExposeConservativeDefaults()
    {
        var options = new ApplicationHostOptions();

        Assert.Null(options.ApplicationName);
        Assert.Null(options.ApplicationId);
        Assert.Null(options.ApplicationVersion);
        Assert.Equal(TimeSpan.FromSeconds(30), options.ShutdownTimeout);
        Assert.Equal(1024, options.DiagnosticsCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildRejectsInvalidDiagnosticsCapacity(int capacity)
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureHost(options => options.DiagnosticsCapacity = capacity);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build());
    }

    [Fact]
    public void BuildRejectsNonPositiveShutdownTimeout()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureHost(options => options.ShutdownTimeout = TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build());
    }

    [Fact]
    public void BuildRequiresExplicitApplicationIdAndRecordsOptionsFailure()
    {
        var builder = ApplicationHost.CreateBuilder();
        var diagnostics = builder.GetBuildDiagnostics();

        var failure = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains(nameof(ApplicationHostOptions.ApplicationId), failure.Message, StringComparison.Ordinal);
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostBuildFailed &&
            record.Context["stage"] == "Options" &&
            record.Context["details"]!.Contains(
                nameof(ApplicationHostOptions.ApplicationId),
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Company.Product")]
    [InlineData("Company.Product ")]
    public void BuildRejectsInvalidApplicationId(string applicationId)
    {
        var builder = ApplicationHost.CreateBuilder();
        builder.ConfigureHost(options => options.ApplicationId = applicationId);

        var failure = Assert.ThrowsAny<Exception>(() => builder.Build());

        Assert.Contains(nameof(ApplicationHostOptions.ApplicationId), failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("nested/name")]
    [InlineData("nested\\name")]
    [InlineData(" Product")]
    [InlineData("Product ")]
    public void BuildRejectsApplicationNameThatIsNotOneDirectorySegment(string applicationName)
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureHost(options => options.ApplicationName = applicationName);

        var failure = Assert.ThrowsAny<Exception>(() => builder.Build());

        Assert.Contains(nameof(ApplicationHostOptions.ApplicationName), failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("LPT1.txt")]
    [InlineData("Product.")]
    public void BuildRejectsWindowsReservedApplicationDirectoryNames(string applicationName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureHost(options => options.ApplicationName = applicationName);

        Assert.Throws<ArgumentException>(() => builder.Build());
    }

    [Fact]
    public async Task ExplicitApplicationVersionOverridesAssemblyMetadata()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureHost(options => options.ApplicationVersion = "2.3.4-preview.1");

        await using var host = builder.Build();

        Assert.Equal("2.3.4-preview.1", host.Context.ApplicationVersion);
    }

    [Fact]
    public async Task ApplicationVersionFallsBackToEntryAssemblyMetadata()
    {
        var entryAssembly = Assembly.GetEntryAssembly()!;
        var expectedVersion = entryAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? entryAssembly.GetName().Version!.ToString();

        await using var host = ApplicationHostTestBuilder.Create().Build();

        Assert.Equal(expectedVersion, host.Context.ApplicationVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" 2.0.0")]
    [InlineData("2.0.0 ")]
    public void BuildRejectsInvalidExplicitApplicationVersion(string applicationVersion)
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureHost(options => options.ApplicationVersion = applicationVersion);

        var failure = Assert.ThrowsAny<Exception>(() => builder.Build());

        Assert.Contains(
            nameof(ApplicationHostOptions.ApplicationVersion),
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplicationInstanceIdIsUniquePerBuild()
    {
        await using var first = ApplicationHostTestBuilder.Create().Build();
        await using var second = ApplicationHostTestBuilder.Create().Build();

        Assert.NotEqual(Guid.Empty, first.Context.ApplicationInstanceId);
        Assert.NotEqual(Guid.Empty, second.Context.ApplicationInstanceId);
        Assert.NotEqual(first.Context.ApplicationInstanceId, second.Context.ApplicationInstanceId);
    }

    [Fact]
    public async Task AppDataPathUsesLocalApplicationDataAndDoesNotCreateDirectory()
    {
        var applicationName = $"AtomUICityPathTest-{Guid.NewGuid():N}";
        var expectedPath = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            applicationName));
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureHost(options => options.ApplicationName = applicationName);

        Assert.False(Directory.Exists(expectedPath));

        await using var host = builder.Build();

        Assert.Equal(Path.TrimEndingDirectorySeparator(expectedPath), host.Context.AppDataPath);
        Assert.True(Path.IsPathFullyQualified(host.Context.AppDataPath));
        Assert.False(Directory.Exists(host.Context.AppDataPath));
    }
}
