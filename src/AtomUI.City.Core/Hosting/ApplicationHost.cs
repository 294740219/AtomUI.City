namespace AtomUI.City.Core.Hosting;

/// <summary>
/// Represents application host.
/// </summary>
public static class ApplicationHost
{
    /// <summary>
    /// Executes the create builder operation.
    /// </summary>
    public static IApplicationHostBuilder CreateBuilder(string[]? args = null)
    {
        return new ApplicationHostBuilder(args ?? []);
    }
}
