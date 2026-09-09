using AtomUI.City.Core.Hosting;

namespace AtomUICityApplication;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var builder = ApplicationHost.CreateBuilder(args);
            builder.ConfigureHost(options =>
            {
                options.ApplicationId = "AtomUICityApplication";
                options.ApplicationName = "AtomUICityApplication";
            });

            await using var host = builder.Build();
            await host.RunAsync();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
