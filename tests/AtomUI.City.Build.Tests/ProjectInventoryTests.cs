using System.Xml.Linq;

namespace AtomUI.City.Build.Tests;

public sealed class ProjectInventoryTests
{
    [Fact]
    public void SolutionIncludesEverySourceAndTestProject()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var solutionProjects = ReadSolutionProjects(repositoryRoot);
        var projectFiles = RepositoryPaths
            .EnumerateRepositoryProjects(repositoryRoot)
            .Select(path => RepositoryPaths.ToRepositoryRelativePath(repositoryRoot, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(projectFiles, solutionProjects);
    }

    [Fact]
    public void EverySourceProjectHasAMatchingTestProject()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var testProjectNames = RepositoryPaths
            .EnumerateTestProjects(repositoryRoot)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToHashSet(StringComparer.Ordinal);

        var expectedTestProjectNames = RepositoryPaths
            .EnumerateSourceProjects(repositoryRoot)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Select(GetExpectedTestProjectName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.All(expectedTestProjectNames, testProjectName => Assert.Contains(testProjectName, testProjectNames));
    }

    [Fact]
    public void SolutionDoesNotIncludeReferenceProjects()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var solutionProjects = ReadSolutionProjects(repositoryRoot);

        Assert.DoesNotContain(solutionProjects, project => project.StartsWith(".referenceprojects/", StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectInventoryGateChecksSolutionCoverageAndOrphanTests()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "engineering", "check-project-inventory.sh");

        Assert.True(File.Exists(scriptPath), "Expected project inventory gate at engineering/check-project-inventory.sh.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("AtomUICity.slnx", script, StringComparison.Ordinal);
        Assert.Contains("find_repository_projects", script, StringComparison.Ordinal);
        Assert.Contains("find_source_projects", script, StringComparison.Ordinal);
        Assert.Contains("find_test_projects", script, StringComparison.Ordinal);
        Assert.Contains("project missing from solution", script, StringComparison.Ordinal);
        Assert.Contains("source project without test project", script, StringComparison.Ordinal);
        Assert.Contains("test project without source project", script, StringComparison.Ordinal);
        Assert.Contains("AtomUI.City.TemplateSmokeTests", script, StringComparison.Ordinal);
        Assert.Contains("src/AtomUI.City.Templates/templates", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectInventoryGateRejectsPlaceholderSourceProjects()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "engineering", "check-project-inventory.sh");

        Assert.True(File.Exists(scriptPath), "Expected project inventory gate at engineering/check-project-inventory.sh.");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("source project without implementation files", script, StringComparison.Ordinal);
        Assert.Contains("has_source_project_implementation", script, StringComparison.Ordinal);
    }

    [Fact]
    public void EverySourceProjectContainsImplementationFilesOrPackAssets()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var placeholderProjects = RepositoryPaths
            .EnumerateSourceProjects(repositoryRoot)
            .Where(projectPath => !HasSourceProjectImplementation(projectPath))
            .Select(projectPath => RepositoryPaths.ToRepositoryRelativePath(repositoryRoot, projectPath))
            .ToArray();

        Assert.Empty(placeholderProjects);
    }

    private static string[] ReadSolutionProjects(string repositoryRoot)
    {
        var solutionPath = Path.Combine(repositoryRoot, "AtomUICity.slnx");
        var solution = XDocument.Load(solutionPath);

        return solution
            .Descendants("Project")
            .Select(project => project.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!.Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetExpectedTestProjectName(string sourceProjectName)
    {
        return sourceProjectName switch
        {
            "AtomUI.City.Templates" => "AtomUI.City.TemplateSmokeTests",
            _ => $"{sourceProjectName}.Tests",
        };
    }

    private static bool HasSourceProjectImplementation(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var implementationFiles = Directory
            .EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path =>
                path.EndsWith(".cs", StringComparison.Ordinal) ||
                path.EndsWith(".props", StringComparison.Ordinal) ||
                path.EndsWith(".targets", StringComparison.Ordinal) ||
                path.EndsWith(".template.config/template.json", StringComparison.Ordinal))
            .ToArray();

        return implementationFiles.Length > 0;
    }
}
