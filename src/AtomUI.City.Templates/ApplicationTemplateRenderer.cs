using System.Text;

namespace AtomUI.City.Templates;

public sealed class ApplicationTemplateRenderer
{
    private const string AtomUICityPackageVersion = "1.0.0";
    private const string MicrosoftNetTestSdkVersion = "17.14.1";
    private const string XUnitVersion = "2.9.3";
    private const string XUnitRunnerVisualStudioVersion = "3.1.4";
    private static readonly object RenderGatesSyncRoot = new();
    private static readonly Dictionary<string, RenderGate> RenderGates = new(GetPathComparer());

    public TemplatePlan CreatePlan(ApplicationTemplateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var diagnostics = options.Validate();
        if (diagnostics.Count > 0)
        {
            throw new ArgumentException(
                $"Template options are invalid: {string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.Code))}.",
                nameof(options));
        }

        var files = CreateFiles(options);
        return CreatePlan(options, files);
    }

    private static TemplatePlan CreatePlan(
        ApplicationTemplateOptions options,
        IReadOnlyList<TemplateFile> files)
    {
        var rootNamespace = options.EffectiveRootNamespace;
        var testTargets = options.IncludeTests
            ? new[] { $"tests/{options.AppName}.Tests/{options.AppName}.Tests.csproj" }
            : [];

        return new TemplatePlan(
            operationId: $"new-app-{options.AppName}",
            command: "atomui city new app",
            inputs: new Dictionary<string, object?>
            {
                ["appName"] = options.AppName,
                ["rootNamespace"] = rootNamespace,
                ["targetFramework"] = options.TargetFramework,
                ["includeTests"] = options.IncludeTests,
                ["useAot"] = options.UseAot,
                ["useDynamicPlugins"] = options.UseDynamicPlugins,
                ["includeSample"] = options.IncludeSample,
            },
            changes: files.Select(static file => file.Change).ToArray(),
            buildTargets: [$"src/{options.AppName}/{options.AppName}.csproj"],
            testTargets: testTargets,
            docsRequired: [$"docs/{options.AppName}.md"],
            risks: options.UseDynamicPlugins ? ["dynamic-plugin-runtime"] : [],
            rollback: files.Select(static file => file.Change.Path).Reverse().ToArray());
    }

    public TemplateRenderResult Render(ApplicationTemplateOptions options)
    {
        return Render(options, CancellationToken.None);
    }

    public TemplateRenderResult Render(ApplicationTemplateOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var optionDiagnostics = options.Validate();
        if (optionDiagnostics.Count > 0)
        {
            return TemplateRenderResult.Failed([.. optionDiagnostics]);
        }

        var files = CreateFiles(options);
        var plan = CreatePlan(options, files);
        var planDiagnostics = plan.Validate();
        if (planDiagnostics.Count > 0)
        {
            return TemplateRenderResult.Failed(plan, [.. planDiagnostics]);
        }

        var rootPath = Path.GetFullPath(options.OutputPath);
        var gate = AcquireRenderGate(rootPath);
        try
        {
            lock (gate.SyncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var file in files)
                {
                    string destination;
                    try
                    {
                        destination = ResolvePath(rootPath, file.Change.Path);
                        ValidateExistingAncestors(rootPath, destination);
                    }
                    catch (Exception exception) when (
                        exception is IOException or UnauthorizedAccessException or NotSupportedException)
                    {
                        return TemplateRenderResult.Failed(
                            plan,
                            CreateOutputDiagnostic(
                                "AUCTPL1005",
                                $"Template output preflight failed: {exception.GetType().Name}.",
                                options,
                                file.Change.Path,
                                rootPath,
                                exception));
                    }

                    if (File.Exists(destination) || Directory.Exists(destination))
                    {
                        return TemplateRenderResult.Failed(
                            plan,
                            CreateOutputDiagnostic(
                                "AUCTPL1004",
                                "Template output already exists.",
                                options,
                                file.Change.Path,
                                destination));
                    }
                }

                var createdFiles = new List<string>();
                var createdDirectories = new List<string>();
                string? currentRelativePath = null;
                try
                {
                    foreach (var file in files)
                    {
                        currentRelativePath = file.Change.Path;
                        cancellationToken.ThrowIfCancellationRequested();
                        var destination = ResolvePath(rootPath, file.Change.Path);
                        EnsureDirectory(rootPath, Path.GetDirectoryName(destination)!, createdDirectories);
                        WriteNewFile(destination, file.Content, createdFiles);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    return TemplateRenderResult.Success(
                        plan,
                        files.Select(static file => file.Change.Path).ToArray());
                }
                catch (OperationCanceledException)
                {
                    Rollback(createdFiles, createdDirectories);
                    throw;
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    var rollbackFailures = Rollback(createdFiles, createdDirectories);
                    var diagnostics = new List<TemplateDiagnostic>
                    {
                        CreateOutputDiagnostic(
                            "AUCTPL1005",
                            $"Template output failed: {exception.GetType().Name}.",
                            options,
                            currentRelativePath,
                            rootPath,
                            exception),
                    };
                    diagnostics.AddRange(rollbackFailures);
                    return TemplateRenderResult.Failed(plan, [.. diagnostics]);
                }
            }
        }
        finally
        {
            ReleaseRenderGate(rootPath, gate);
        }
    }

    private static IReadOnlyList<TemplateFile> CreateFiles(ApplicationTemplateOptions options)
    {
        var files = new List<TemplateFile>
        {
            CreateFile($"{options.AppName}.slnx", CreateSolution(options)),
            CreateFile("Directory.Build.props", CreateDirectoryBuildProps()),
            CreateFile("Directory.Packages.props", CreateDirectoryPackagesProps()),
            CreateFile($"docs/{options.AppName}.md", CreateDocsEntry(options)),
            CreateFile($"src/{options.AppName}/{options.AppName}.csproj", CreateApplicationProject(options)),
            CreateFile($"src/{options.AppName}/Program.cs", CreateProgram(options)),
            CreateFile($"src/{options.AppName}/Modules/.gitkeep", string.Empty),
            CreateFile($"src/{options.AppName}/Routes/.gitkeep", string.Empty),
            CreateFile($"src/{options.AppName}/Resources/.gitkeep", string.Empty),
            CreateFile($"src/{options.AppName}/Configuration/.gitkeep", string.Empty),
            CreateFile($"src/{options.AppName}/Localization/.gitkeep", string.Empty),
        };

        if (options.IncludeTests)
        {
            files.Add(CreateFile(
                $"tests/{options.AppName}.Tests/{options.AppName}.Tests.csproj",
                CreateTestProject(options)));
            files.Add(CreateFile(
                $"tests/{options.AppName}.Tests/FeatureTestMatrix.md",
                CreateFeatureTestMatrix(options)));
            files.Add(CreateFile(
                $"tests/{options.AppName}.Tests/ApplicationSmokeTests.cs",
                CreateApplicationSmokeTests(options)));
        }

        if (options.IncludeSample)
        {
            files.Add(CreateFile(
                $"src/{options.AppName}/Samples/WelcomeViewModel.cs",
                CreateWelcomeViewModel(options)));
        }

        return files.AsReadOnly();
    }

    private static TemplateFile CreateFile(string path, string content)
    {
        return new TemplateFile(TemplateChange.Create(path), content);
    }

    private static string ResolvePath(string rootPath, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine([rootPath, .. relativePath.Split('/')]));
        var rootPrefix = Path.TrimEndingDirectorySeparator(rootPath) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, GetPathComparison()))
        {
            throw new IOException("Template output escaped its target root.");
        }

        return path;
    }

    private static void EnsureDirectory(
        string rootPath,
        string directory,
        List<string> createdDirectories)
    {
        var missing = new Stack<string>();
        for (var current = directory;
             !Directory.Exists(current);
             current = Path.GetDirectoryName(current) ?? throw new IOException("Template directory has no parent."))
        {
            if (File.Exists(current))
            {
                throw new IOException("A file blocks a template output directory.");
            }

            missing.Push(current);
            if (string.Equals(current, rootPath, GetPathComparison()))
            {
                break;
            }
        }

        while (missing.TryPop(out var path))
        {
            Directory.CreateDirectory(path);
            createdDirectories.Add(path);
        }
    }

    private static void ValidateExistingAncestors(string rootPath, string destination)
    {
        for (var current = Path.GetDirectoryName(destination);
             current is not null && !string.Equals(current, rootPath, GetPathComparison());
             current = Path.GetDirectoryName(current))
        {
            if (File.Exists(current))
            {
                throw new IOException("A file blocks a template output directory.");
            }

            if (!Directory.Exists(current))
            {
                continue;
            }

            var directory = new DirectoryInfo(current);
            if (directory.LinkTarget is not null ||
                directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IOException("Template output cannot traverse a symbolic link or reparse point.");
            }
        }
    }

    private static void WriteNewFile(string path, string content, List<string> createdFiles)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        createdFiles.Add(path);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static IReadOnlyList<TemplateDiagnostic> Rollback(
        IReadOnlyList<string> createdFiles,
        IReadOnlyList<string> createdDirectories)
    {
        var diagnostics = new List<TemplateDiagnostic>();
        foreach (var path in createdFiles.Reverse())
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(CreateRollbackDiagnostic(path, exception));
            }
        }

        foreach (var path in createdDirectories.Reverse())
        {
            try
            {
                if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                {
                    Directory.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(CreateRollbackDiagnostic(path, exception));
            }
        }

        return diagnostics;
    }

    private static TemplateDiagnostic CreateRollbackDiagnostic(string path, Exception exception)
    {
        return new TemplateDiagnostic(
            "AUCTPL1006",
            $"Template rollback failed: {exception.GetType().Name}.",
            new Dictionary<string, object?>
            {
                ["templateId"] = "atomui-city-app",
                ["path"] = path,
                ["errorType"] = exception.GetType().FullName,
            });
    }

    private static TemplateDiagnostic CreateOutputDiagnostic(
        string code,
        string message,
        ApplicationTemplateOptions options,
        string? relativePath,
        string targetPath,
        Exception? exception = null)
    {
        return new TemplateDiagnostic(
            code,
            message,
            new Dictionary<string, object?>
            {
                ["templateId"] = "atomui-city-app",
                ["targetPath"] = targetPath,
                ["path"] = relativePath,
                ["operationId"] = $"new-app-{options.AppName}",
                ["errorType"] = exception?.GetType().FullName,
            });
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    }

    private static RenderGate AcquireRenderGate(string rootPath)
    {
        lock (RenderGatesSyncRoot)
        {
            if (!RenderGates.TryGetValue(rootPath, out var gate))
            {
                gate = new RenderGate();
                RenderGates.Add(rootPath, gate);
            }

            gate.ReferenceCount++;
            return gate;
        }
    }

    private static void ReleaseRenderGate(string rootPath, RenderGate gate)
    {
        lock (RenderGatesSyncRoot)
        {
            gate.ReferenceCount--;
            if (gate.ReferenceCount == 0 &&
                RenderGates.TryGetValue(rootPath, out var registered) &&
                ReferenceEquals(registered, gate))
            {
                RenderGates.Remove(rootPath);
            }
        }
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    private static string CreateSolution(ApplicationTemplateOptions options)
    {
        var testProject = options.IncludeTests
            ? $$"""
                  <Folder Name="/tests/">
                    <Project Path="tests/{{options.AppName}}.Tests/{{options.AppName}}.Tests.csproj" />
                  </Folder>
            """
            : string.Empty;

        return $$"""
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/{{options.AppName}}/{{options.AppName}}.csproj" />
              </Folder>
            {{testProject}}
            </Solution>
            """;
    }

    private static string CreateDirectoryBuildProps()
    {
        return """
            <Project>

              <PropertyGroup>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <LangVersion>latest</LangVersion>
                <WarningsAsErrors>Nullable</WarningsAsErrors>
              </PropertyGroup>

            </Project>
            """;
    }

    private static string CreateDirectoryPackagesProps()
    {
        return """
            <Project>

              <PropertyGroup>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
              </PropertyGroup>

            </Project>
            """;
    }

    private static string CreateDocsEntry(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;

        return $$"""
            # {{options.AppName}}

            Generated by `atomui city new app`.

            | Field | Value |
            | --- | --- |
            | Root namespace | `{{rootNamespace}}` |
            | Target framework | `{{options.TargetFramework}}` |
            | Tests included | `{{options.IncludeTests.ToString().ToLowerInvariant()}}` |

            ## Restore, Build, Test

            ```bash
            dotnet restore {{options.AppName}}.slnx
            dotnet build {{options.AppName}}.slnx --no-restore
            dotnet test {{options.AppName}}.slnx --no-build
            ```
            """;
    }

    private static string CreateApplicationProject(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;
        var dynamicPlugins = options.UseDynamicPlugins
            ? """
                <PackageReference Include="AtomUI.City.PluginSystem" Version="{{AtomUICityPackageVersion}}" />
            """
            : string.Empty;

        return $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>{{options.TargetFramework}}</TargetFramework>
                <RootNamespace>{{rootNamespace}}</RootNamespace>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <AtomUICityManifestGeneration>true</AtomUICityManifestGeneration>
                <AtomUICityAotFriendly>{{options.UseAot.ToString().ToLowerInvariant()}}</AtomUICityAotFriendly>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="AtomUI.City.Build" Version="{{AtomUICityPackageVersion}}" PrivateAssets="all" />
                <PackageReference Include="AtomUI.City.Core" Version="{{AtomUICityPackageVersion}}" />
                <PackageReference Include="AtomUI.City.Mvvm" Version="{{AtomUICityPackageVersion}}" />
                <PackageReference Include="AtomUI.City.Routing" Version="{{AtomUICityPackageVersion}}" />
                <PackageReference Include="AtomUI.City.Localization" Version="{{AtomUICityPackageVersion}}" />
            {{dynamicPlugins}}
              </ItemGroup>

            </Project>
            """;
    }

    private static string CreateProgram(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;

        return $$"""
            using AtomUI.City.Core.Hosting;

            namespace {{rootNamespace}};

            internal static class Program
            {
                public static async Task<int> Main(string[] args)
                {
                    try
                    {
                        return await RunAsync(args);
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLine(exception);
                        return 1;
                    }
                }

                private static async Task<int> RunAsync(string[] args)
                {
                    var builder = ApplicationHost.CreateBuilder(args);
                    builder.ConfigureHost(options =>
                    {
                        options.ApplicationId = "{{rootNamespace}}";
                        options.ApplicationName = "{{options.AppName}}";
                    });

                    await using var host = builder.Build();

                    await host.RunAsync();

                    return 0;
                }
            }
            """;
    }

    private static string CreateTestProject(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;

        return $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{{options.TargetFramework}}</TargetFramework>
                <RootNamespace>{{rootNamespace}}.Tests</RootNamespace>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <IsPackable>false</IsPackable>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="{{MicrosoftNetTestSdkVersion}}" />
                <PackageReference Include="xunit" Version="{{XUnitVersion}}" />
                <PackageReference Include="xunit.runner.visualstudio" Version="{{XUnitRunnerVisualStudioVersion}}" PrivateAssets="all" />
              </ItemGroup>

              <ItemGroup>
                <ProjectReference Include="../../src/{{options.AppName}}/{{options.AppName}}.csproj" />
              </ItemGroup>

              <ItemGroup>
                <Using Include="Xunit" />
              </ItemGroup>

            </Project>
            """;
    }

    private static string CreateFeatureTestMatrix(ApplicationTemplateOptions options)
    {
        return $$"""
            # {{options.AppName}} Feature Test Matrix

            | Feature | Unit Tests | Integration Tests | Notes |
            |---|---|---|---|
            | Application startup | ApplicationSmokeTests | Pending | Generated by AtomUI.City template. |
            """;
    }

    private static string CreateApplicationSmokeTests(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;

        return $$"""
            using AtomUI.City.Core.Hosting;
            using AtomUI.City.Core.Lifecycle;

            namespace {{rootNamespace}}.Tests;

            public sealed class ApplicationSmokeTests
            {
                [Fact]
                public async Task ApplicationHostStartsAndStops()
                {
                    var builder = ApplicationHost.CreateBuilder();
                    builder.ConfigureHost(hostOptions =>
                    {
                        hostOptions.ApplicationId = "{{rootNamespace}}.Tests";
                        hostOptions.ApplicationName = "{{options.AppName}} Tests";
                    });

                    await using var host = builder.Build();
                    await host.StartAsync();
                    Assert.Equal(LifecycleScopeState.Running, host.HostScope.State);

                    await host.StopAsync();
                    Assert.Equal(LifecycleScopeState.Stopped, host.HostScope.State);
                }
            }
            """;
    }

    private static string CreateWelcomeViewModel(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;
        return $$"""
            using AtomUI.City.Mvvm;

            namespace {{rootNamespace}}.Samples;

            public sealed class WelcomeViewModel : ViewModelBase
            {
                private string _message = "AtomUI.City";

                public string Message
                {
                    get => _message;
                    set => SetProperty(ref _message, value);
                }
            }
            """;
    }

    private sealed record TemplateFile(TemplateChange Change, string Content);

    private sealed class RenderGate
    {
        public object SyncRoot { get; } = new();

        public int ReferenceCount { get; set; }
    }
}
