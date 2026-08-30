using System.Diagnostics;
using System.Text.Json;

namespace AtomUI.City.Core.Tests;

public sealed class CoreHeadlessProcessTests
{
    [Fact]
    public async Task HeadlessLifecycleRunsToCompletionWithoutUiRuntime()
    {
        var result = await RunScenarioAsync("lifecycle");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(1, result.GetProperty("hostedStartCount").GetInt32());
        Assert.Equal(1, result.GetProperty("hostedStopCount").GetInt32());
        Assert.True(result.GetProperty("scopedDisposed").GetBoolean());
        Assert.Equal("Disposed", result.GetProperty("hostScopeState").GetString());
        Assert.Equal("Disposed", result.GetProperty("applicationScopeState").GetString());
        Assert.Contains("AUCHOST002", ReadStrings(result, "diagnostics"));
        Assert.Contains("AUCHOST003", ReadStrings(result, "diagnostics"));
    }

    [Fact]
    public async Task HeadlessStartupFailureReturnsOriginalErrorAfterRollback()
    {
        var result = await RunScenarioAsync("startup-failure");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(typeof(InvalidOperationException).FullName, result.GetProperty("errorType").GetString());
        Assert.Equal(
            ["foundation:init", "failing:init", "failing:shutdown", "foundation:shutdown"],
            ReadStrings(result, "calls"));
        Assert.Contains("AUCHOST102", ReadStrings(result, "diagnostics"));
    }

    [Fact]
    public async Task HeadlessShutdownAggregatesFailuresAndStillStopsHostedServices()
    {
        var result = await RunScenarioAsync("shutdown-failure");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(2, result.GetProperty("failureCount").GetInt32());
        Assert.Equal(1, result.GetProperty("hostedStopCount").GetInt32());
        Assert.Equal(["second:shutdown", "first:shutdown"], ReadStrings(result, "calls"));
        Assert.Contains("AUCHOST103", ReadStrings(result, "diagnostics"));
    }

    [Fact]
    public async Task HeadlessRunCancellationStillExecutesShutdown()
    {
        var result = await RunScenarioAsync("run-cancellation");

        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("canceled").GetBoolean());
        Assert.Equal(1, result.GetProperty("hostedStopCount").GetInt32());
        Assert.Contains("cancellation:shutdown", ReadStrings(result, "calls"));
    }

    private static async Task<JsonElement> RunScenarioAsync(string scenario)
    {
        var repositoryRoot = FindRepositoryRoot();
        var applicationPath = Path.Combine(
            repositoryRoot,
            "output",
            "bin",
            "Debug",
            "AtomUI.City.Core.HeadlessApp",
            "net10.0",
            "AtomUI.City.Core.HeadlessApp.dll");

        Assert.True(File.Exists(applicationPath), $"Headless fixture was not built: {applicationPath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(applicationPath);
        startInfo.ArgumentList.Add(scenario);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the headless Core fixture.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var standardError = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Headless Core scenario '{scenario}' did not exit.");
        }

        var output = await standardOutput;
        var error = await standardError;
        Assert.True(
            process.ExitCode == 0,
            $"Headless Core scenario '{scenario}' exited with {process.ExitCode}. stdout: {output} stderr: {error}");

        var jsonLine = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(jsonLine));

        using var document = JsonDocument.Parse(jsonLine);
        return document.RootElement.Clone();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AtomUICity.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }

    private static string[] ReadStrings(JsonElement result, string propertyName)
    {
        return result
            .GetProperty(propertyName)
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
    }
}
