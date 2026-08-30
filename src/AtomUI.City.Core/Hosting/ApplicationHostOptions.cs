namespace AtomUI.City.Core.Hosting;

public sealed class ApplicationHostOptions
{
    public string? ApplicationName { get; set; }

    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public int DiagnosticsCapacity { get; set; } = 1024;

    public bool AllowDynamicDiscovery { get; set; }
}
