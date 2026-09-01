namespace AtomUI.City.Core.Hosting;

internal sealed class ApplicationContext : IApplicationContext
{
    public ApplicationContext(
        string applicationId,
        Guid applicationInstanceId,
        string applicationName,
        string applicationVersion,
        string environmentName,
        string contentRootPath,
        string appDataPath,
        IReadOnlyList<string> startupArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataPath);
        ArgumentNullException.ThrowIfNull(startupArguments);

        if (applicationInstanceId == Guid.Empty)
        {
            throw new ArgumentException(
                "Application instance id cannot be empty.",
                nameof(applicationInstanceId));
        }

        ApplicationId = applicationId;
        ApplicationInstanceId = applicationInstanceId;
        ApplicationName = applicationName;
        ApplicationVersion = applicationVersion;
        EnvironmentName = environmentName;
        ContentRootPath = contentRootPath;
        AppDataPath = appDataPath;
        StartupArguments = Array.AsReadOnly(startupArguments.ToArray());
    }

    public string ApplicationId { get; }

    public Guid ApplicationInstanceId { get; }

    public string ApplicationName { get; }

    public string ApplicationVersion { get; }

    public string EnvironmentName { get; }

    public string ContentRootPath { get; }

    public string AppDataPath { get; }

    public IReadOnlyList<string> StartupArguments { get; }
}
