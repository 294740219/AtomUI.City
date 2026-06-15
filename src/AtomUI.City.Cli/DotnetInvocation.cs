namespace AtomUI.City.Cli;

public sealed class DotnetInvocation
{
    private DotnetInvocation(IReadOnlyList<string> arguments)
        : this(arguments, Directory.GetCurrentDirectory(), ciMode: false)
    {
    }

    private DotnetInvocation(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        bool ciMode)
    {
        Arguments = Array.AsReadOnly(arguments.ToArray());
        WorkingDirectory = workingDirectory;
        CiMode = ciMode;
    }

    public string Executable { get; } = "dotnet";

    public IReadOnlyList<string> Arguments { get; }

    public string WorkingDirectory { get; }

    public bool CiMode { get; }

    internal static DotnetInvocation Create(string command, CliCommandLine commandLine)
    {
        return Create(command, commandLine, Directory.GetCurrentDirectory());
    }

    internal static DotnetInvocation Create(
        string command,
        CliCommandLine commandLine,
        string workingDirectory)
    {
        var arguments = new List<string> { command };
        var project = commandLine.GetOptionValue("--project");
        if (!string.IsNullOrWhiteSpace(project))
        {
            arguments.Add(project);
        }

        var configuration = commandLine.GetOptionValue("--configuration");
        if (!string.IsNullOrWhiteSpace(configuration))
        {
            arguments.Add("--configuration");
            arguments.Add(configuration);
        }

        var framework = commandLine.GetOptionValue("--framework");
        if (!string.IsNullOrWhiteSpace(framework))
        {
            arguments.Add("--framework");
            arguments.Add(framework);
        }

        return new DotnetInvocation(arguments, workingDirectory, commandLine.HasOption("--ci"));
    }
}
