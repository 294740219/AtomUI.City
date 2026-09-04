using AtomUI.City.Core.Hosting;

namespace Quickstart;

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
        var builder = ApplicationHost.CreateBuilder(args);
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "Quickstart";
            options.ApplicationName = "Quickstart";
        });

        await using var host = builder.Build();

        await host.RunAsync();

        return 0;
    }
}
