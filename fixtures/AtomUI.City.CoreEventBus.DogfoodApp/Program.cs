using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Modularity;
using AtomUI.City.CoreEventBus.DogfoodApp.Modules;
using AtomUI.City.Fixtures;

namespace AtomUI.City.CoreEventBus.DogfoodApp;

internal static class Program
{
    public static Task<int> Main(string[] args) => ProcessEntryPoint.RunAsync(() => RunAsync(args));

    private static async Task<int> RunAsync(string[] args)
    {
        var scenario = args.Length == 0 ? "verify-all" : args[0];
        var builder = ApplicationHost.CreateBuilder(args);
        builder.UseModule<DogfoodApplicationModule>();
        builder.ConfigureHost(options =>
        {
            options.ApplicationId = "AtomUI.City.CoreEventBus.DogfoodApp";
            options.ApplicationName = "Core + EventBus Dogfood CLI";
        });

        await using var host = builder.Build();
        await host.StartAsync();
        var result = await DogfoodScenarioRunner.RunAsync(host, scenario);
        if (!result.HostStopped)
        {
            await host.StopAsync();
        }

        Console.WriteLine(result.ToJson());
        return 0;
    }
}
