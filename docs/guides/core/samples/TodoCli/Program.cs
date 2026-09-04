using Microsoft.Extensions.DependencyInjection;

namespace TodoCli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await RunAsync(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        var builder = TodoHost.CreateBuilder(args);

        await using var host = builder.Build();

        await host.StartAsync();

        var runner = host.Services.GetRequiredService<CliRunner>();
        var exitCode = runner.ExitCode;

        await host.StopAsync();

        return exitCode;
    }
}
