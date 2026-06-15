using System.Xml.Linq;

namespace AtomUI.City.Build.Tests;

public sealed class SourceGeneratorProjectStructureTests
{
    [Fact]
    public void GeneratorsProjectUsesDedicatedSourceGeneratorLayout()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var generatorRoot = Path.Combine(repositoryRoot, "src", "AtomUI.City.Generators");

        Assert.True(File.Exists(Path.Combine(generatorRoot, "AtomUI.City.Generators.csproj")));

        AssertDirectoryExists(generatorRoot, "Common");
        AssertDirectoryExists(generatorRoot, "Diagnostics");
        AssertDirectoryExists(generatorRoot, "EventBus");
        AssertDirectoryExists(generatorRoot, "Localization");
        AssertDirectoryExists(generatorRoot, "Manifest");
        AssertDirectoryExists(generatorRoot, "Modularity");
        AssertDirectoryExists(generatorRoot, "PluginSystem");
        AssertDirectoryExists(generatorRoot, "Presentation");
        AssertDirectoryExists(generatorRoot, "Routing");
        AssertDirectoryExists(generatorRoot, "Security");
    }

    [Fact]
    public void GeneratorsProjectHasDedicatedTestProject()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();

        Assert.True(File.Exists(Path.Combine(repositoryRoot, "tests", "AtomUI.City.Generators.Tests", "AtomUI.City.Generators.Tests.csproj")));
    }

    [Fact]
    public void RuntimeProjectsDoNotReferenceGeneratorsProject()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var runtimeProjects = RepositoryPaths
            .EnumerateSourceProjects(repositoryRoot)
            .Where(path => Path.GetFileNameWithoutExtension(path) is not "AtomUI.City.Build"
                and not "AtomUI.City.Cli"
                and not "AtomUI.City.Generators"
                and not "AtomUI.City.Templates"
                and not "AtomUI.City.Testing");

        foreach (var projectPath in runtimeProjects)
        {
            var text = File.ReadAllText(projectPath);

            Assert.DoesNotContain("AtomUI.City.Generators.csproj", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GeneratorPackageUsesAnalyzerOnlyPackageLayout()
    {
        var repositoryRoot = RepositoryPaths.FindRepositoryRoot();
        var generatorProject = XDocument.Load(Path.Combine(repositoryRoot, "src", "AtomUI.City.Generators", "AtomUI.City.Generators.csproj"));
        var validatePackagesScript = File.ReadAllText(Path.Combine(repositoryRoot, "engineering", "validate-packages.sh"));

        var properties = generatorProject
            .Descendants()
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToDictionary(element => element.Name.LocalName, element => element.Value.Trim(), StringComparer.Ordinal);

        Assert.Equal("netstandard2.0", properties["TargetFramework"]);
        Assert.Equal("false", properties["IncludeBuildOutput"]);
        Assert.Equal("true", properties["SuppressDependenciesWhenPacking"]);
        Assert.Contains("analyzers/dotnet/cs/$project_name.dll", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("analyzers/dotnet/cs/$project_name.pdb", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("reject_entry_pattern", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("^lib/", validatePackagesScript, StringComparison.Ordinal);
        Assert.Contains("Generator package contains runtime lib asset", validatePackagesScript, StringComparison.Ordinal);
    }

    private static void AssertDirectoryExists(string root, string relativePath)
    {
        Assert.True(Directory.Exists(Path.Combine(root, relativePath)), $"Expected generator directory '{relativePath}'.");
    }
}
