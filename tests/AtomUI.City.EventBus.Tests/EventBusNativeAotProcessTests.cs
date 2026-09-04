using AtomUI.City.Testing.Processes;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventBusNativeAotProcessTests
{
    [Theory]
    [InlineData("net8.0")]
    [InlineData("net10.0")]
    public async Task GeneratedCatalogPublishesAndRunsAsNativeAot(string targetFramework)
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "fixtures", "AtomUI.City.EventBus.HeadlessApp",
            "AtomUI.City.EventBus.HeadlessApp.csproj");
        var output = Path.Combine(root, "output", "eventbus-aot-tests", targetFramework);
        var isolatedIntermediateRoot = Path.Combine(root, "output", "eventbus-aot-intermediate", targetFramework);
        var processDirectory = Path.GetTempPath();
        var publish = await RunAsync("dotnet", processDirectory,
            "publish", project, "-c", "Release", "-r", "win-x64", "--self-contained", "true",
            $"-p:AtomUICityDevelopTargetFramework={targetFramework}",
            $"-p:AtomUICityIsolatedIntermediateRoot={isolatedIntermediateRoot}",
            "-p:AtomUICityEventBusPublishAot=true", "-o", output);

        Assert.True(publish.ExitCode == 0, publish.Output);
        var executable = Path.Combine(output, "AtomUI.City.EventBus.HeadlessApp.exe");
        Assert.True(File.Exists(executable), publish.Output);

        var run = await RunAsync(executable, processDirectory);

        Assert.True(run.ExitCode == 0, run.Output);
        Assert.Contains("EVENTBUS_AOT_OK", run.Output, StringComparison.Ordinal);

        var failure = await RunAsync(executable, processDirectory, "--test-entry-failure");
        Assert.NotEqual(0, failure.ExitCode);
        Assert.Contains(typeof(InvalidOperationException).FullName!, failure.Output, StringComparison.Ordinal);
        Assert.Contains("EventBus fixture entry failure.", failure.Output, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName,
        string workingDirectory,
        params string[] arguments)
    {
        var result = await ProcessTestRunner.RunAsync(
            fileName,
            workingDirectory,
            TimeSpan.FromMinutes(5),
            arguments);
        return (
            result.ExitCode,
            result.StandardOutput + Environment.NewLine + result.StandardError);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AtomUICity.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("AtomUI.City repository root was not found.");
    }
}
