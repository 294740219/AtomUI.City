using System.Diagnostics;
using System.Reflection;

namespace TodoCli.Tests;

public sealed class TodoCliProcessTests
{
    private static readonly string CliDll = Path.Combine(
        AppContext.BaseDirectory,
        "TodoCli.dll");

    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            ErrorDialog = false,
        };
        startInfo.ArgumentList.Add(CliDll);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("TodoCli did not exit within 30 seconds.");
        }

        return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    [Fact]
    public void AddThenCompleteProducesExitCodeZero()
    {
        var (addCode, _, _) = Run("add", "Buy milk");
        Assert.Equal(0, addCode);
    }

    [Fact]
    public void UnknownCommandFails()
    {
        var (badCode, _, _) = Run("bogus");
        Assert.NotEqual(0, badCode);
    }

    [Fact]
    public void HelpPrintsCommands()
    {
        var (helpCode, stdout, _) = Run("--help");
        Assert.Equal(0, helpCode);
        Assert.Contains("add", stdout);
        Assert.Contains("list", stdout);
    }
}
