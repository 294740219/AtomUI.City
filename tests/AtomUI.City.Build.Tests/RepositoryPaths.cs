namespace AtomUI.City.Build.Tests;

internal static class RepositoryPaths
{
    private const string TemplatePayloadRoot = "src/AtomUI.City.Templates/templates/";

    public static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "AtomUICity.slnx");

            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate AtomUICity.slnx from the test output directory.");
    }

    public static string ToRepositoryRelativePath(string repositoryRoot, string path)
    {
        return Path
            .GetRelativePath(repositoryRoot, path)
            .Replace('\\', '/');
    }

    public static IEnumerable<string> EnumerateRepositoryProjects(string repositoryRoot)
    {
        return Directory
            .EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => IsRepositoryProject(repositoryRoot, path));
    }

    public static IEnumerable<string> EnumerateSourceProjects(string repositoryRoot)
    {
        return Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsTemplatePayloadProject(repositoryRoot, path));
    }

    public static IEnumerable<string> EnumerateTestProjects(string repositoryRoot)
    {
        return Directory.EnumerateFiles(Path.Combine(repositoryRoot, "tests"), "*.csproj", SearchOption.AllDirectories);
    }

    public static bool IsRepositoryProject(string repositoryRoot, string projectPath)
    {
        var relativePath = ToRepositoryRelativePath(repositoryRoot, projectPath);

        return (relativePath.StartsWith("benchmarks/", StringComparison.Ordinal) ||
                relativePath.StartsWith("src/", StringComparison.Ordinal) ||
                relativePath.StartsWith("tests/", StringComparison.Ordinal)) &&
               !relativePath.StartsWith(TemplatePayloadRoot, StringComparison.Ordinal);
    }

    private static bool IsTemplatePayloadProject(string repositoryRoot, string projectPath)
    {
        return ToRepositoryRelativePath(repositoryRoot, projectPath)
            .StartsWith(TemplatePayloadRoot, StringComparison.Ordinal);
    }
}
