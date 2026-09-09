using System.Diagnostics;

namespace AtomUI.City.Data.Tests;

public sealed class DataDogfoodTests
{
    [Fact]
    public async Task RealLocalHttpGrpcAndSignalRFixturePasses()
    {
        var assembly = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "AtomUI.City.Data.HeadlessApp",
            "net10.0",
            "AtomUI.City.Data.HeadlessApp.dll");
        assembly = Path.GetFullPath(assembly);
        Assert.True(File.Exists(assembly), $"Headless fixture was not built: {assembly}");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{assembly}\"",
            WorkingDirectory = Path.GetDirectoryName(assembly)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Data headless fixture process could not be started.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Data headless fixture did not complete within 30 seconds.");
        }

        var output = await outputTask;
        var error = await errorTask;
        Assert.True(process.ExitCode == 0, $"Fixture failed with exit code {process.ExitCode}.{Environment.NewLine}{error}");
        Assert.Contains("DATA_HEADLESS_OK", output, StringComparison.Ordinal);
    }
}
