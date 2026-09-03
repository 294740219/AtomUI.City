namespace AtomUI.City.Core.Hosting;

/// <summary>
/// Represents application host options.
/// </summary>
public sealed class ApplicationHostOptions
{
    /// <summary>
    /// Gets or sets the application id value.
    /// </summary>
    public string? ApplicationId { get; set; }

    /// <summary>
    /// Gets or sets the application name value.
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets the application version value.
    /// </summary>
    public string? ApplicationVersion { get; set; }

    /// <summary>
    /// Gets or sets the shutdown timeout value.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the diagnostics capacity value.
    /// </summary>
    public int DiagnosticsCapacity { get; set; } = 1024;
}
