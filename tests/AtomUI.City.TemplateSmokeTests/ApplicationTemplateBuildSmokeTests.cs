using System.Diagnostics;
using AtomUI.City.Templates;

namespace AtomUI.City.TemplateSmokeTests;

public sealed class ApplicationTemplateBuildSmokeTests
{
    [Fact]
    public void TemplatePlanCollectionsRejectExternalMutation()
    {
        var plan = new TemplatePlan(
            "new-app-SalesClient",
            "atomui city new app",
            new Dictionary<string, object?> { ["appName"] = "SalesClient" },
            [TemplateChange.Create("src/SalesClient/SalesClient.csproj")]);
        var inputs = Assert.IsAssignableFrom<IDictionary<string, object?>>(plan.Inputs);
        var changes = Assert.IsAssignableFrom<IList<TemplateChange>>(plan.Changes);

        Assert.Throws<NotSupportedException>(() => inputs["appName"] = "Changed");
        Assert.Throws<NotSupportedException>(() => changes[0] = TemplateChange.Create("changed"));
        Assert.Equal("SalesClient", plan.Inputs["appName"]);
        Assert.Equal("src/SalesClient/SalesClient.csproj", plan.Changes[0].Path);
    }

    [Fact]
    public void TemplateRenderDiagnosticsRejectExternalListMutation()
    {
        var result = TemplateRenderResult.Failed(
            new TemplateDiagnostic("AUCTPL0001", "First"),
            new TemplateDiagnostic("AUCTPL0002", "Second"));
        var diagnostics = Assert.IsAssignableFrom<IList<TemplateDiagnostic>>(result.Diagnostics);

        Assert.Throws<NotSupportedException>(() => diagnostics[0] = new TemplateDiagnostic("AUCTPL9999", "Changed"));
        Assert.Equal("AUCTPL0001", result.Diagnostics[0].Code);
        Assert.Equal("AUCTPL0002", result.Diagnostics[1].Code);
    }

    [Fact]
    public void ApplicationTemplateGeneratesBuildAndTestProjectFiles()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            TargetFramework = "net10.0",
            IncludeTests = true,
        });

        var appProjectPath = Path.Combine(workspace.Root, "src", "SalesClient", "SalesClient.csproj");
        var testProjectPath = Path.Combine(workspace.Root, "tests", "SalesClient.Tests", "SalesClient.Tests.csproj");
        var programPath = Path.Combine(workspace.Root, "src", "SalesClient", "Program.cs");

        Assert.True(File.Exists(appProjectPath), $"Expected application project at {appProjectPath}.");
        Assert.True(File.Exists(testProjectPath), $"Expected test project at {testProjectPath}.");

        var appProject = File.ReadAllText(appProjectPath);
        Assert.Contains("<ImplicitUsings>enable</ImplicitUsings>", appProject, StringComparison.Ordinal);
        Assert.Contains("<Nullable>enable</Nullable>", appProject, StringComparison.Ordinal);
        Assert.Contains("""<PackageReference Include="AtomUI.City.Core" Version="0.1.0" />""", appProject, StringComparison.Ordinal);
        Assert.Contains("""<PackageReference Include="AtomUI.City.Build" Version="0.1.0" PrivateAssets="all" />""", appProject, StringComparison.Ordinal);

        var testProject = File.ReadAllText(testProjectPath);
        Assert.Contains("""<PackageReference Include="Microsoft.NET.Test.Sdk" Version=""", testProject, StringComparison.Ordinal);
        Assert.Contains("""<PackageReference Include="xunit" Version=""", testProject, StringComparison.Ordinal);
        Assert.Contains("""<ProjectReference Include="../../src/SalesClient/SalesClient.csproj" />""", testProject, StringComparison.Ordinal);

        var program = File.ReadAllText(programPath);
        Assert.Contains("using AtomUI.City.Hosting;", program, StringComparison.Ordinal);
        Assert.Contains("ApplicationHost.CreateBuilder(args)", program, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationTemplateGeneratesSolutionBuildPropsAndDocsEntry()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            TargetFramework = "net10.0",
            IncludeTests = true,
        });

        var solutionPath = Path.Combine(workspace.Root, "SalesClient.slnx");
        var directoryBuildPropsPath = Path.Combine(workspace.Root, "Directory.Build.props");
        var docsEntryPath = Path.Combine(workspace.Root, "docs", "SalesClient.md");

        Assert.True(File.Exists(solutionPath), $"Expected generated solution at {solutionPath}.");
        Assert.True(File.Exists(directoryBuildPropsPath), $"Expected Directory.Build.props at {directoryBuildPropsPath}.");
        Assert.True(File.Exists(docsEntryPath), $"Expected docs entry at {docsEntryPath}.");

        var solution = File.ReadAllText(solutionPath);
        Assert.Contains("""<Project Path="src/SalesClient/SalesClient.csproj" />""", solution, StringComparison.Ordinal);
        Assert.Contains("""<Project Path="tests/SalesClient.Tests/SalesClient.Tests.csproj" />""", solution, StringComparison.Ordinal);

        var directoryBuildProps = File.ReadAllText(directoryBuildPropsPath);
        Assert.Contains("<Nullable>enable</Nullable>", directoryBuildProps, StringComparison.Ordinal);
        Assert.Contains("<LangVersion>latest</LangVersion>", directoryBuildProps, StringComparison.Ordinal);

        var docsEntry = File.ReadAllText(docsEntryPath);
        Assert.Contains("Company.SalesClient", docsEntry, StringComparison.Ordinal);
        Assert.Contains("atomui city new app", docsEntry, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationTemplateGeneratedFilesDoNotContainAbsoluteWorkspacePaths()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            TargetFramework = "net10.0",
            IncludeTests = true,
        });

        foreach (var file in Directory.EnumerateFiles(workspace.Root, "*", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain(workspace.Root, content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ApplicationTemplateGeneratedSolutionCanRestoreBuildAndTest()
    {
        using var workspace = new TemplateSmokeWorkspace();
        var renderer = new ApplicationTemplateRenderer();

        renderer.Render(new ApplicationTemplateOptions
        {
            AppName = "SalesClient",
            RootNamespace = "Company.SalesClient",
            OutputPath = workspace.Root,
            TargetFramework = "net10.0",
            IncludeTests = true,
        });

        var solutionPath = Path.Combine(workspace.Root, "SalesClient.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected generated solution at {solutionPath}.");

        var repositoryRoot = FindRepositoryRoot();
        var packageSource = Path.Combine(repositoryRoot.FullName, "output", "NuGet", "Debug");
        Assert.True(Directory.Exists(packageSource), $"Expected local package source at {packageSource}.");

        await RunDotnetAsync(workspace.Root, "restore", "SalesClient.slnx", "--source", packageSource, "--source", "https://api.nuget.org/v3/index.json");
        await RunDotnetAsync(workspace.Root, "build", "SalesClient.slnx", "--no-restore");
        await RunDotnetAsync(workspace.Root, "test", "SalesClient.slnx", "--no-build");
    }

    [Fact]
    public void ApplicationTemplateRenderObservesPreCancelledTokenBeforeWriting()
    {
        using var workspace = new TemplateSmokeWorkspace();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var renderer = new ApplicationTemplateRenderer();

        Assert.Throws<OperationCanceledException>(() => renderer.Render(
            new ApplicationTemplateOptions
            {
                AppName = "SalesClient",
                RootNamespace = "Company.SalesClient",
                OutputPath = workspace.Root,
                TargetFramework = "net10.0",
                IncludeTests = true,
            },
            cancellation.Token));
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "src", "SalesClient")));
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "tests", "SalesClient.Tests")));
    }

    private static async Task RunDotnetAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.True(
            process.ExitCode == 0,
            $"""
            dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.
            STDOUT:
            {stdout}
            STDERR:
            {stderr}
            """);
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

    private sealed class TemplateSmokeWorkspace : IDisposable
    {
        public TemplateSmokeWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "AtomUICityTemplateSmokeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
