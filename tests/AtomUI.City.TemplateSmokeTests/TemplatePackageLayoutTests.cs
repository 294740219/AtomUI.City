using System.Text.Json;
using System.Xml.Linq;
using AtomUI.City.Templates;

namespace AtomUI.City.TemplateSmokeTests;

public sealed class TemplatePackageLayoutTests
{
    [Fact]
    public void ApplicationTemplatePlanIncludesRequiredFilesWithNormalizedRelativePaths()
    {
        var renderer = new ApplicationTemplateRenderer();

        var plan = renderer.CreatePlan(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = "out",
            TargetFramework = "net10.0",
            IncludeTests = true,
        });

        Assert.Contains(plan.Changes, change => change.Path == "SalesClient.slnx");
        Assert.Contains(plan.Changes, change => change.Path == "Directory.Build.props");
        Assert.Contains(plan.Changes, change => change.Path == "docs/SalesClient.md");
        Assert.Contains(plan.Changes, change => change.Path == "src/SalesClient/SalesClient.csproj");
        Assert.Contains(plan.Changes, change => change.Path == "tests/SalesClient.Tests/SalesClient.Tests.csproj");
        Assert.All(plan.Changes, change =>
        {
            Assert.Equal("create", change.Type);
            Assert.DoesNotContain('\\', change.Path);
            Assert.False(Path.IsPathRooted(change.Path), $"Expected relative path, got {change.Path}.");
            Assert.DoesNotContain("..", change.Path.Split('/'));
        });
        Assert.Empty(plan.Validate());
    }

    [Fact]
    public void TemplateChangeCreateNormalizesPathSeparatorsAndSegments()
    {
        var change = TemplateChange.Create(@"src\SalesClient\.\Program.cs");

        Assert.Equal("src/SalesClient/Program.cs", change.Path);
    }

    [Fact]
    public void TemplateChangeCreateRejectsPathEscapes()
    {
        Assert.Throws<ArgumentException>(() => TemplateChange.Create("../outside.txt"));
    }

    [Fact]
    public void TemplatePlanValidateReportsDuplicateNormalizedPath()
    {
        var plan = new TemplatePlan(
            "new-app-SalesClient",
            "atomui city new app",
            new Dictionary<string, object?> { ["appName"] = "SalesClient" },
            [
                new TemplateChange("create", "src/SalesClient/Program.cs"),
                new TemplateChange("create", @"src\SalesClient\.\Program.cs"),
            ]);

        var diagnostic = Assert.Single(plan.Validate());

        Assert.Equal("AUCTPL1002", diagnostic.Code);
        Assert.Equal("src/SalesClient/Program.cs", diagnostic.Context["normalizedPath"]);
        Assert.Equal(@"src\SalesClient\.\Program.cs", diagnostic.Context["path"]);
    }

    [Fact]
    public void TemplatePlanValidateReportsPathEscape()
    {
        var plan = new TemplatePlan(
            "new-app-SalesClient",
            "atomui city new app",
            new Dictionary<string, object?> { ["appName"] = "SalesClient" },
            [new TemplateChange("create", "../outside.txt")]);

        var diagnostic = Assert.Single(plan.Validate());

        Assert.Equal("AUCTPL1001", diagnostic.Code);
        Assert.Equal("../outside.txt", diagnostic.Context["path"]);
    }

    [Fact]
    public void TemplatePlanValidateReportsUnsupportedChangeType()
    {
        var plan = new TemplatePlan(
            "new-app-SalesClient",
            "atomui city new app",
            new Dictionary<string, object?> { ["appName"] = "SalesClient" },
            [new TemplateChange("delete", "src/SalesClient/Program.cs")]);

        var diagnostic = Assert.Single(plan.Validate());

        Assert.Equal("AUCTPL1003", diagnostic.Code);
        Assert.Equal("delete", diagnostic.Context["type"]);
        Assert.Equal("src/SalesClient/Program.cs", diagnostic.Context["path"]);
    }

    [Fact]
    public void TemplateRootDirectoryExists()
    {
        var repositoryRoot = FindRepositoryRoot();
        var templateRoot = Path.Combine(repositoryRoot.FullName, "src", "AtomUI.City.Templates", "templates");

        Assert.True(Directory.Exists(templateRoot), $"Expected template root directory at {templateRoot}.");
    }

    [Theory]
    [InlineData("atomui-city-app")]
    [InlineData("atomui-city-plugin")]
    public void TemplateMetadataExists(string templateName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var templateJson = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "AtomUI.City.Templates",
            "templates",
            templateName,
            ".template.config",
            "template.json");

        Assert.True(File.Exists(templateJson), $"Expected template metadata at {templateJson}.");
    }

    [Theory]
    [InlineData("atomui-city-app", "AtomUI.City.Templates.Application")]
    [InlineData("atomui-city-plugin", "AtomUI.City.Templates.Plugin")]
    public void TemplateMetadataDefinesStablePackageIdentity(string templateName, string identity)
    {
        var metadata = ReadTemplateMetadata(templateName);

        Assert.Equal(identity, metadata.RootElement.GetProperty("identity").GetString());
        Assert.Equal(templateName, metadata.RootElement.GetProperty("shortName").GetString());
        Assert.False(string.IsNullOrWhiteSpace(metadata.RootElement.GetProperty("sourceName").GetString()));
    }

    [Fact]
    public void PluginTemplatePackageContainsPluginProjectManifestModuleAndTests()
    {
        var root = GetTemplateRoot("atomui-city-plugin");

        Assert.True(File.Exists(Path.Combine(root, "src", "AtomUICityPlugin", "AtomUICityPlugin.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "src", "AtomUICityPlugin", "AtomUICityPluginModule.cs")));
        Assert.True(File.Exists(Path.Combine(root, "src", "AtomUICityPlugin", "atomui-city", "plugin.json")));
        Assert.True(File.Exists(Path.Combine(root, "tests", "AtomUICityPlugin.Tests", "AtomUICityPlugin.Tests.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "tests", "AtomUICityPlugin.Tests", "PluginPackageTests.cs")));
        Assert.True(File.Exists(Path.Combine(root, "tests", "AtomUICityPlugin.Tests", "FeatureTestMatrix.md")));
    }

    [Fact]
    public void PluginTemplateProjectDefinesSingleAssemblyNuGetAndMsBuildMetadata()
    {
        var projectPath = Path.Combine(GetTemplateRoot("atomui-city-plugin"), "src", "AtomUICityPlugin", "AtomUICityPlugin.csproj");
        var project = XDocument.Load(projectPath);
        var properties = project
            .Descendants("PropertyGroup")
            .Elements()
            .ToDictionary(element => element.Name.LocalName, element => element.Value, StringComparer.Ordinal);
        var packageReferences = project
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal("AtomUICityPlugin", properties["PackageId"]);
        Assert.Equal("true", properties["AtomUICityPlugin"]);
        Assert.Equal("PluginId", properties["AtomUICityPluginId"]);
        Assert.Equal("1.0.0", properties["AtomUICityPluginVersion"]);
        Assert.Equal("Plugin.DisplayName", properties["AtomUICityPluginDisplayNameKey"]);
        Assert.Equal("Plugin.Description", properties["AtomUICityPluginDescriptionKey"]);
        Assert.Equal("true", properties["AtomUICityPluginGenerateManifest"]);
        Assert.Equal("true", properties["AtomUICityPluginValidateManifest"]);
        Assert.Equal("true", properties["AtomUICityPackageAsPlugin"]);
        Assert.Contains("AtomUI.City.Build", packageReferences);
        Assert.Contains("AtomUI.City.Core", packageReferences);
        Assert.Contains("AtomUI.City.PluginSystem", packageReferences);
    }

    [Fact]
    public void PluginTemplateManifestMatchesSingleAssemblyContract()
    {
        var manifestPath = Path.Combine(GetTemplateRoot("atomui-city-plugin"), "src", "AtomUICityPlugin", "atomui-city", "plugin.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;

        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("PluginId", root.GetProperty("pluginId").GetString());
        Assert.Equal("AtomUICityPlugin", root.GetProperty("packageId").GetString());
        Assert.Equal("AtomUICityPlugin.dll", root.GetProperty("mainAssembly").GetString());
        Assert.Equal("net10.0", root.GetProperty("targetFramework").GetString());
        Assert.Equal("1.0", root.GetProperty("pluginApiVersion").GetString());
        Assert.Equal("AtomUICityPluginModule", root.GetProperty("modules")[0].GetProperty("name").GetString());
        Assert.Equal("AtomUICityPlugin.AtomUICityPluginModule", root.GetProperty("modules")[0].GetProperty("type").GetString());
    }

    [Fact]
    public void PluginTemplateTestProjectReferencesPluginAndTestingPackage()
    {
        var testProjectPath = Path.Combine(GetTemplateRoot("atomui-city-plugin"), "tests", "AtomUICityPlugin.Tests", "AtomUICityPlugin.Tests.csproj");
        var project = XDocument.Load(testProjectPath);
        var packageReferences = project
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var projectReference = Assert.Single(project.Descendants("ProjectReference"));

        Assert.Equal("../../src/AtomUICityPlugin/AtomUICityPlugin.csproj", projectReference.Attribute("Include")?.Value);
        Assert.Contains("AtomUI.City.Testing", packageReferences);
        Assert.Contains("Microsoft.NET.Test.Sdk", packageReferences);
        Assert.Contains("xunit", packageReferences);
    }

    private static string GetTemplateRoot(string templateName)
    {
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(
            repositoryRoot.FullName,
            "src",
            "AtomUI.City.Templates",
            "templates",
            templateName);
    }

    private static JsonDocument ReadTemplateMetadata(string templateName)
    {
        var templateJson = Path.Combine(
            GetTemplateRoot(templateName),
            ".template.config",
            "template.json");

        return JsonDocument.Parse(File.ReadAllText(templateJson));
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AtomUICity.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
