using System.Diagnostics;
using System.Text;

namespace AtomUI.City.Cli;

internal static class ProcessRunner
{
    private const int OutputCaptureLimit = 4096;
    private const string TruncationSuffix = "\n[truncated]";

    public static async ValueTask<ProcessRunResult> RunAsync(
        DotnetInvocation invocation,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var startInfo = new ProcessStartInfo
        {
            FileName = invocation.Executable,
            WorkingDirectory = invocation.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (invocation.CiMode)
        {
            startInfo.Environment["CI"] = "true";
            startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        }

        foreach (var argument in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new ProcessRunResult(CliExitCodes.Failure, string.Empty, "Failed to start dotnet process.");
        }

        try
        {
            var outputTask = ReadToLimitAsync(process.StandardOutput, cancellationToken);
            var errorTask = ReadToLimitAsync(process.StandardError, cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            return new ProcessRunResult(
                process.ExitCode,
                await outputTask.ConfigureAwait(false),
                await errorTask.ConfigureAwait(false),
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
    }

    private static async ValueTask<string> ReadToLimitAsync(
        TextReader reader,
        CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        var builder = new StringBuilder(OutputCaptureLimit);
        var truncated = false;

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            var available = OutputCaptureLimit - builder.Length;
            if (available > 0)
            {
                builder.Append(buffer, 0, Math.Min(read, available));
            }

            if (read > available)
            {
                truncated = true;
            }
        }

        return truncated ? builder.Append(TruncationSuffix).ToString() : builder.ToString();
    }
}

internal sealed record ProcessRunResult(int ExitCode, string Output, string Error, long DurationMs = 0);
