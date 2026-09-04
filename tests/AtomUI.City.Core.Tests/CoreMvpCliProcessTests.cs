using System.Text.Json;
using AtomUI.City.Testing.Processes;

namespace AtomUI.City.Core.Tests;

[Trait("Category", "Dogfood")]
[Collection(ProcessTestCollection.Name)]
public sealed class CoreMvpCliProcessTests
{
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    [Fact]
    public async Task CoreMvpEntryFailureIsReportedWithoutUnhandledExceptionEscape()
    {
        var result = await RunProcessAsync("--test-entry-failure");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(typeof(InvalidOperationException).FullName!, result.StandardError, StringComparison.Ordinal);
        Assert.Contains("Core MVP fixture entry failure.", result.StandardError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("baseline")]
    [InlineData("permutations")]
    [InlineData("combinations")]
    [InlineData("policies")]
    [InlineData("concurrency")]
    [InlineData("isolation")]
    [InlineData("conflict")]
    public async Task CoreMvpScenarioPassesAsProductProcess(string scenario)
    {
        var result = await RunAsync("verify", "--scenario", scenario);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Json.GetProperty("success").GetBoolean(), string.Join(
            Environment.NewLine,
            result.Json.GetProperty("failures").EnumerateArray().Select(item => item.GetString())));
    }

    [Fact]
    public async Task CoreMvpAllRunsCompleteIndustrialMatrix()
    {
        var result = await RunAsync("verify", "--scenario", "all");

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Json.GetProperty("success").GetBoolean());
        Assert.Equal(6, result.Json.GetProperty("selectedModuleCount").GetInt32());
        Assert.Equal(27, result.Json.GetProperty("selectedServiceCount").GetInt32());
        Assert.Equal(120, result.Json.GetProperty("permutationCount").GetInt32());
        Assert.Equal(32, result.Json.GetProperty("combinationCount").GetInt32());
        Assert.Equal(64, result.Json.GetProperty("concurrentScopeCount").GetInt32());
        Assert.Empty(result.Json.GetProperty("failures").EnumerateArray());
    }

    [Fact]
    public async Task CoreMvpRejectsInvalidCommandWithMachineReadableResult()
    {
        var result = await RunAsync("unknown");

        Assert.Equal(2, result.ExitCode);
        Assert.False(result.Json.GetProperty("success").GetBoolean());
        Assert.Equal(2, result.Json.GetProperty("exitCode").GetInt32());
    }

    private static async Task<ProcessResult> RunAsync(params string[] arguments)
    {
        var processResult = await RunProcessAsync(arguments);
        var output = processResult.StandardOutput;
        var error = processResult.StandardError;
        var jsonLine = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(jsonLine), $"Core MVP CLI returned no JSON. stderr: {error}");
        using var document = JsonDocument.Parse(jsonLine);
        return new ProcessResult(processResult.ExitCode, document.RootElement.Clone());
    }

    private static Task<TestProcessResult> RunProcessAsync(params string[] arguments)
    {
        var executable = Path.Combine(
            FindRepositoryRoot(),
            "output",
            "bin",
            BuildConfiguration,
            "AtomUI.City.Core.MvpCli",
            "net10.0",
            "AtomUI.City.Core.MvpCli.dll");
        Assert.True(File.Exists(executable), $"Core MVP CLI was not built: {executable}");
        return ProcessTestRunner.RunAsync(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            FindRepositoryRoot(),
            TimeSpan.FromMinutes(2),
            [executable, .. arguments]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AtomUICity.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ProcessResult(int ExitCode, JsonElement Json);
}
