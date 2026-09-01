namespace AtomUI.City.Core.Hosting;

public sealed class ApplicationHostOptions
{
    public string? ApplicationId { get; set; }

    public string? ApplicationName { get; set; }

    public string? ApplicationVersion { get; set; }

    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public int DiagnosticsCapacity { get; set; } = 1024;
}
