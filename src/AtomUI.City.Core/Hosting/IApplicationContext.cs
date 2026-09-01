namespace AtomUI.City.Core.Hosting;

public interface IApplicationContext
{
    string ApplicationId { get; }

    Guid ApplicationInstanceId { get; }

    string ApplicationName { get; }

    string ApplicationVersion { get; }

    string EnvironmentName { get; }

    string ContentRootPath { get; }

    string AppDataPath { get; }

    IReadOnlyList<string> StartupArguments { get; }
}
