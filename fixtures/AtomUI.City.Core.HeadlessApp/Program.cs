using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.HeadlessApp;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args is ["--test-scenario", var scenario])
        {
            return await HeadlessTestScenarios.RunAsync(scenario);
        }

        var builder = ApplicationHost.CreateBuilder(args);
        await using var host = builder.Build();

        Console.WriteLine("Starting...");
        await host.StartAsync();

        Console.WriteLine("Stopping...");
        await host.StopAsync();

        Console.WriteLine("Stopped...");
        return 0;
    }
}
