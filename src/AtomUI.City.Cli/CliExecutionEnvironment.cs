namespace AtomUI.City.Cli;

public sealed class CliExecutionEnvironment
{
    public CliExecutionEnvironment(
        string workingDirectory,
        bool isCi = false,
        bool isNonInteractive = false,
        bool isStdinAvailable = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        WorkingDirectory = workingDirectory;
        IsCi = isCi;
        IsStdinAvailable = isStdinAvailable;
        IsNonInteractive = isNonInteractive || isCi || !isStdinAvailable;
    }

    public string WorkingDirectory { get; }

    public bool IsCi { get; }

    public bool IsNonInteractive { get; }

    public bool IsStdinAvailable { get; }
}
