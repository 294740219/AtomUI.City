namespace AtomUI.City.Core.Hosting;

/// <summary>
/// Defines the contract for iapplication context.
/// </summary>
public interface IApplicationContext
{
    /// <summary>
    /// Gets the application id value.
    /// </summary>
    string ApplicationId { get; }

    /// <summary>
    /// Gets the application instance id value.
    /// </summary>
    Guid ApplicationInstanceId { get; }

    /// <summary>
    /// Gets the application name value.
    /// </summary>
    string ApplicationName { get; }

    /// <summary>
    /// Gets the application version value.
    /// </summary>
    string ApplicationVersion { get; }

    /// <summary>
    /// Gets the environment name value.
    /// </summary>
    string EnvironmentName { get; }

    /// <summary>
    /// Gets the content root path value.
    /// </summary>
    string ContentRootPath { get; }

    /// <summary>
    /// Gets the app data path value.
    /// </summary>
    string AppDataPath { get; }

    /// <summary>
    /// Gets the startup arguments value.
    /// </summary>
    IReadOnlyList<string> StartupArguments { get; }
}
