using System.Reflection;
using System.Text.Json;
using AtomUI.City.Cli;

namespace AtomUI.City.Cli.Tests;

public sealed class CliBuildAndTestCommandTests
{
    private const int LongOutputLength = 5000;

    [Fact]
    public async Task BuildDryRunEmitsDotnetBuildInvocation()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(
            "city",
            "build",
            "--configuration",
            "Release",
            "--project",
            "src/App/App.csproj",
            "--dry-run",
            "--json");

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        var invocation = json.RootElement.GetProperty("data").GetProperty("invocation");
        Assert.Equal("dotnet", invocation.GetProperty("executable").GetString());
        Assert.Equal("build", invocation.GetProperty("arguments")[0].GetString());
        Assert.Contains(
            invocation.GetProperty("arguments").EnumerateArray(),
            argument => argument.GetString() == "Release");
    }

    [Fact]
    public async Task TestDryRunEmitsDotnetTestInvocation()
    {
        using var host = new CliTestHost();

        var run = await host.RunAsync(
            "city",
            "test",
            "--project",
            "tests/App.Tests/App.Tests.csproj",
            "--dry-run",
            "--json");

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        var invocation = json.RootElement.GetProperty("data").GetProperty("invocation");
        Assert.Equal("dotnet", invocation.GetProperty("executable").GetString());
        Assert.Equal("test", invocation.GetProperty("arguments")[0].GetString());
    }

    [Fact]
    public void DotnetInvocationArgumentsRejectExternalMutation()
    {
        var constructor = typeof(DotnetInvocation).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(IReadOnlyList<string>)]);
        var invocation = Assert.IsType<DotnetInvocation>(
            constructor?.Invoke(
                [
                    new[]
                    {
                        "build",
                        "src/App/App.csproj",
                        "--configuration",
                        "Release",
                    },
                ]));
        var arguments = Assert.IsAssignableFrom<IList<string>>(invocation.Arguments);

        Assert.Throws<NotSupportedException>(() => arguments[0] = "changed");
        Assert.Equal("build", invocation.Arguments[0]);
        Assert.Equal("src/App/App.csproj", invocation.Arguments[1]);
    }

    [Fact]
    public async Task BuildCommandRunsProcessInWorkingDirectoryAndReturnsOutputSummary()
    {
        using var host = new CliTestHost();
        DotnetInvocation? captured = null;

        var run = await RunWithRunnerAsync(
            host,
            [
                "city",
                "build",
                "--configuration",
                "Release",
                "--json",
            ],
            (invocation, _) =>
            {
                captured = invocation;
                return ValueTask.FromResult(new ProcessRunResult(0, "build ok", string.Empty));
            });

        Assert.Equal(0, run.ExitCode);
        Assert.NotNull(captured);
        Assert.Equal(host.WorkingDirectory, captured.WorkingDirectory);
        using var json = run.ReadJson();
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(0, data.GetProperty("exitCode").GetInt32());
        Assert.Equal("build ok", data.GetProperty("stdout").GetString());
        Assert.Equal(host.WorkingDirectory, data.GetProperty("invocation").GetProperty("workingDirectory").GetString());
    }

    [Fact]
    public async Task TestCommandMapsNonZeroExitCodeAndStderrSummary()
    {
        using var host = new CliTestHost();

        var run = await RunWithRunnerAsync(
            host,
            [
                "city",
                "test",
                "--project",
                "tests/App.Tests/App.Tests.csproj",
                "--json",
            ],
            (_, _) => ValueTask.FromResult(new ProcessRunResult(7, "test output", "test failed")));

        Assert.Equal(7, run.ExitCode);
        using var json = run.ReadJson();
        var root = json.RootElement;
        var data = root.GetProperty("data");
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("AUCCLI0201", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Equal(7, data.GetProperty("exitCode").GetInt32());
        Assert.Equal("test failed", data.GetProperty("stderr").GetString());
    }

    [Fact]
    public async Task BuildCommandCancellationReturnsStableEnvelope()
    {
        using var host = new CliTestHost();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var run = await RunWithRunnerAsync(
            host,
            [
                "city",
                "build",
                "--json",
            ],
            (_, token) => throw new OperationCanceledException(token),
            cancellation.Token);

        Assert.Equal(1, run.ExitCode);
        using var json = run.ReadJson();
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("AUCCLI0202", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task BuildCommandCiModePassesCiFlagToRunner()
    {
        using var host = new CliTestHost();
        DotnetInvocation? captured = null;

        var run = await RunWithRunnerAsync(
            host,
            [
                "city",
                "build",
                "--ci",
                "--json",
            ],
            (invocation, _) =>
            {
                captured = invocation;
                return ValueTask.FromResult(new ProcessRunResult(0, string.Empty, string.Empty));
            });

        Assert.Equal(0, run.ExitCode);
        Assert.NotNull(captured);
        Assert.True(captured.CiMode);
        using var json = run.ReadJson();
        Assert.True(json.RootElement.GetProperty("data").GetProperty("invocation").GetProperty("ciMode").GetBoolean());
    }

    [Fact]
    public async Task BuildCommandTruncatesLongOutputInEnvelope()
    {
        using var host = new CliTestHost();

        var run = await RunWithRunnerAsync(
            host,
            [
                "city",
                "build",
                "--json",
            ],
            (_, _) => ValueTask.FromResult(new ProcessRunResult(0, new string('o', LongOutputLength), new string('e', LongOutputLength))));

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        var data = json.RootElement.GetProperty("data");
        var stdout = data.GetProperty("stdout").GetString();
        var stderr = data.GetProperty("stderr").GetString();
        Assert.NotNull(stdout);
        Assert.NotNull(stderr);
        Assert.EndsWith("[truncated]", stdout, StringComparison.Ordinal);
        Assert.EndsWith("[truncated]", stderr, StringComparison.Ordinal);
        Assert.True(stdout.Length < LongOutputLength);
        Assert.True(stderr.Length < LongOutputLength);
    }

    [Fact]
    public async Task BuildCommandRejectsMissingWorkingDirectoryBeforeRunningProcess()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), "AtomUICityCliTests", Guid.NewGuid().ToString("N"));
        var output = new StringWriter();
        var error = new StringWriter();
        var invoked = false;

        var exitCode = await CliApplication.RunAsync(
            [
                "city",
                "build",
                "--json",
            ],
            output,
            error,
            new CliExecutionEnvironment(missingDirectory),
            CancellationToken.None,
            (invocation, cancellationToken) =>
            {
                invoked = true;
                return ValueTask.FromResult(new ProcessRunResult(0, string.Empty, string.Empty));
            });

        Assert.Equal(1, exitCode);
        Assert.False(invoked);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("AUCCLI0203", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    private static async ValueTask<CliTestRun> RunWithRunnerAsync(
        CliTestHost host,
        string[] args,
        Func<DotnetInvocation, CancellationToken, ValueTask<ProcessRunResult>> runner,
        CancellationToken cancellationToken = default)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await CliApplication.RunAsync(
            args,
            output,
            error,
            new CliExecutionEnvironment(host.WorkingDirectory),
            cancellationToken,
            runner);

        return new CliTestRun(exitCode, output.ToString(), error.ToString());
    }
}
