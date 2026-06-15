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
}
