namespace AtomUI.City.Core.Hosting;

public static class ApplicationHost
{
    public static IApplicationHostBuilder CreateBuilder(string[]? args = null)
    {
        return new ApplicationHostBuilder(args ?? []);
    }
}
