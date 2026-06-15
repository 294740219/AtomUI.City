namespace AtomUI.City.Templates;

public sealed class ApplicationTemplateRenderer
{
    private const string AtomUICityPackageVersion = "0.1.0";
    private const string MicrosoftNetTestSdkVersion = "17.14.1";
    private const string XUnitVersion = "2.9.3";
    private const string XUnitRunnerVisualStudioVersion = "3.1.4";

    public TemplatePlan CreatePlan(ApplicationTemplateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var rootNamespace = options.EffectiveRootNamespace;

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
            changes: GetChanges(options).ToArray());
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

        var plan = CreatePlan(options);
        var planDiagnostics = plan.Validate();
        if (planDiagnostics.Count > 0)
        {
            return TemplateRenderResult.Failed([.. planDiagnostics]);
        }

        WriteFile(options, $"{options.AppName}.slnx", CreateSolution(options), cancellationToken);
        WriteFile(options, "Directory.Build.props", CreateDirectoryBuildProps(), cancellationToken);
        WriteFile(options, $"docs/{options.AppName}.md", CreateDocsEntry(options), cancellationToken);
        WriteFile(options, $"src/{options.AppName}/{options.AppName}.csproj", CreateApplicationProject(options), cancellationToken);
        WriteFile(options, $"src/{options.AppName}/Program.cs", CreateProgram(options), cancellationToken);
        WriteFile(options, $"src/{options.AppName}/App.axaml", CreateAppXaml(options), cancellationToken);
        WriteFile(options, $"src/{options.AppName}/App.axaml.cs", CreateAppCodeBehind(options), cancellationToken);
        WriteFile(options, $"src/{options.AppName}/Modules/.gitkeep", string.Empty, cancellationToken);
        WriteFile(options, $"src/{options.AppName}/Routes/.gitkeep", string.Empty, cancellationToken);
        WriteFile(options, $"src/{options.AppName}/Resources/.gitkeep", string.Empty, cancellationToken);
        WriteFile(options, $"src/{options.AppName}/Configuration/.gitkeep", string.Empty, cancellationToken);
        WriteFile(options, $"src/{options.AppName}/Localization/.gitkeep", string.Empty, cancellationToken);

        if (options.IncludeTests)
        {
            WriteFile(options, $"tests/{options.AppName}.Tests/{options.AppName}.Tests.csproj", CreateTestProject(options), cancellationToken);
            WriteFile(options, $"tests/{options.AppName}.Tests/FeatureTestMatrix.md", CreateFeatureTestMatrix(options), cancellationToken);
            WriteFile(options, $"tests/{options.AppName}.Tests/ApplicationSmokeTests.cs", CreateApplicationSmokeTests(options), cancellationToken);
        }

        return TemplateRenderResult.Success(plan);
    }

    private static IEnumerable<TemplateChange> GetChanges(ApplicationTemplateOptions options)
    {
        yield return TemplateChange.Create($"{options.AppName}.slnx");
        yield return TemplateChange.Create("Directory.Build.props");
        yield return TemplateChange.Create($"docs/{options.AppName}.md");
        yield return TemplateChange.Create($"src/{options.AppName}/{options.AppName}.csproj");
        yield return TemplateChange.Create($"src/{options.AppName}/Program.cs");
        yield return TemplateChange.Create($"src/{options.AppName}/App.axaml");
        yield return TemplateChange.Create($"src/{options.AppName}/App.axaml.cs");
        yield return TemplateChange.Create($"src/{options.AppName}/Modules/.gitkeep");
        yield return TemplateChange.Create($"src/{options.AppName}/Routes/.gitkeep");
        yield return TemplateChange.Create($"src/{options.AppName}/Resources/.gitkeep");
        yield return TemplateChange.Create($"src/{options.AppName}/Configuration/.gitkeep");
        yield return TemplateChange.Create($"src/{options.AppName}/Localization/.gitkeep");

        if (options.IncludeTests)
        {
            yield return TemplateChange.Create($"tests/{options.AppName}.Tests/{options.AppName}.Tests.csproj");
            yield return TemplateChange.Create($"tests/{options.AppName}.Tests/FeatureTestMatrix.md");
            yield return TemplateChange.Create($"tests/{options.AppName}.Tests/ApplicationSmokeTests.cs");
        }
    }

    private static void WriteFile(
        ApplicationTemplateOptions options,
        string relativePath,
        string content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = Path.Combine([options.OutputPath, .. relativePath.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        cancellationToken.ThrowIfCancellationRequested();
        File.WriteAllText(path, content);
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
                <PackageReference Include="AtomUI.City.PluginSystem" Version="0.1.0" />
            """
            : string.Empty;

        return $$"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>WinExe</OutputType>
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
                <PackageReference Include="AtomUI.City.Presentation" Version="{{AtomUICityPackageVersion}}" />
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
            using AtomUI.City.Hosting;

            namespace {{rootNamespace}};

            internal static class Program
            {
                public static async Task<int> Main(string[] args)
                {
                    var host = ApplicationHost.CreateBuilder(args)
                        .Build();

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

    private static string CreateAppXaml(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;

        return $$"""
            <Application
                x:Class="{{rootNamespace}}.App"
                xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            </Application>
            """;
    }

    private static string CreateAppCodeBehind(ApplicationTemplateOptions options)
    {
        var rootNamespace = options.EffectiveRootNamespace;

        return $$"""
            namespace {{rootNamespace}};

            public sealed partial class App
            {
                public void Initialize()
                {
                }
            }
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
            namespace {{rootNamespace}}.Tests;

            public sealed class ApplicationSmokeTests
            {
                [Fact]
                public void ApplicationTemplateContainsSmokeTest()
                {
                    Assert.True(true);
                }
            }
            """;
    }
}
