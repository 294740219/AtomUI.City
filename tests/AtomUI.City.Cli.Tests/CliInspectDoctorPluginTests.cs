namespace AtomUI.City.Cli.Tests;

public sealed class CliInspectDoctorPluginTests
{
    [Fact]
    public async Task InspectWorkspaceReadsSolutionAndProjects()
    {
        using var host = new CliTestHost();
        Directory.CreateDirectory(Path.Combine(host.WorkingDirectory, "src", "App"));
        File.WriteAllText(
            Path.Combine(host.WorkingDirectory, "AtomUICity.slnx"),
            """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/App/App.csproj" />
              </Folder>
            </Solution>
            """);
        File.WriteAllText(Path.Combine(host.WorkingDirectory, "src", "App", "App.csproj"), "<Project />");

        var run = await host.RunAsync("city", "inspect", "workspace", "--json");

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        var projects = json.RootElement.GetProperty("data").GetProperty("projects");
        Assert.Contains(
            projects.EnumerateArray(),
            project => project.GetProperty("path").GetString() == "src/App/App.csproj");
    }

    [Fact]
    public async Task PluginInstallDryRunEmitsPlan()
    {
        using var host = new CliTestHost();
        var packagePath = Path.Combine(host.WorkingDirectory, "SalesPlugin.1.0.0.nupkg");
        File.WriteAllText(packagePath, "placeholder");

        var run = await host.RunAsync(
            "city",
            "plugin",
            "install",
            packagePath,
            "--plugins-root",
            Path.Combine(host.WorkingDirectory, "plugins"),
            "--dry-run",
            "--json");

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal(
            "install-plugin",
            json.RootElement.GetProperty("data").GetProperty("plan").GetProperty("changes")[0].GetProperty("type").GetString());
        Assert.False(Directory.Exists(Path.Combine(host.WorkingDirectory, "plugins", "installed")));
    }

    [Fact]
    public async Task PluginListReadsInstalledManifest()
    {
        using var host = new CliTestHost();
        var pluginRoot = Path.Combine(
            host.WorkingDirectory,
            "plugins",
            "installed",
            "com.company.sales",
            "1.0.0",
            "root",
            "atomui-city");
        Directory.CreateDirectory(pluginRoot);
        File.WriteAllText(
            Path.Combine(pluginRoot, "plugin.json"),
            """
            {
              "schemaVersion": "1.0",
              "pluginId": "com.company.sales",
              "packageId": "Company.Sales.Plugin",
              "version": "1.0.0",
              "displayNameKey": "Sales.DisplayName",
              "mainAssembly": "Company.Sales.Plugin.dll",
              "targetFramework": "net10.0",
              "pluginApiVersion": "1.0",
              "minHostVersion": "1.0.0"
            }
            """);

        var run = await host.RunAsync(
            "city",
            "plugin",
            "list",
            "--plugins-root",
            Path.Combine(host.WorkingDirectory, "plugins"),
            "--json");

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        var plugins = json.RootElement.GetProperty("data").GetProperty("plugins");
        Assert.Contains(
            plugins.EnumerateArray(),
            plugin => plugin.GetProperty("pluginId").GetString() == "com.company.sales");
    }

    [Fact]
    public async Task PluginInspectReadsValidManifest()
    {
        using var host = new CliTestHost();
        var packageRoot = CreatePluginPackage(host.WorkingDirectory);
        WritePluginManifest(packageRoot);

        var run = await host.RunAsync("city", "plugin", "inspect", packageRoot, "--json");

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("succeeded").GetBoolean());
        Assert.Equal("com.company.sales", data.GetProperty("manifest").GetProperty("pluginId").GetString());
        Assert.Empty(data.GetProperty("pluginDiagnostics").EnumerateArray());
    }

    [Fact]
    public async Task PluginInspectRejectsMissingManifest()
    {
        using var host = new CliTestHost();
        var packageRoot = CreatePluginPackage(host.WorkingDirectory);

        var run = await host.RunAsync("city", "plugin", "inspect", packageRoot, "--json");

        Assert.Equal(1, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCPLG0000", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
        Assert.Equal("AUCPLG0000", json.RootElement.GetProperty("data").GetProperty("pluginDiagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task PluginInspectRejectsInvalidManifestVersion()
    {
        using var host = new CliTestHost();
        var packageRoot = CreatePluginPackage(host.WorkingDirectory);
        WritePluginManifest(packageRoot, version: "invalid");

        var run = await host.RunAsync("city", "plugin", "inspect", packageRoot, "--json");

        Assert.Equal(1, run.ExitCode);
        using var json = run.ReadJson();
        Assert.Equal("AUCPLG0011", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task PluginDoctorReportsMissingMainAssembly()
    {
        using var host = new CliTestHost();
        var packageRoot = CreatePluginPackage(host.WorkingDirectory);
        WritePluginManifest(packageRoot);

        var run = await host.RunAsync("city", "plugin", "doctor", packageRoot, "--json");

        Assert.Equal(1, run.ExitCode);
        using var json = run.ReadJson();
        var data = json.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("succeeded").GetBoolean());
        Assert.Equal("AUCPLG0002", json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task PluginDoctorAcceptsValidPackageLayout()
    {
        using var host = new CliTestHost();
        var packageRoot = CreatePluginPackage(host.WorkingDirectory);
        WritePluginManifest(packageRoot);
        var assemblyPath = Path.Combine(packageRoot, "lib", "net10.0", "Company.Sales.Plugin.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath)!);
        File.WriteAllText(assemblyPath, string.Empty);

        var run = await host.RunAsync("city", "plugin", "doctor", packageRoot, "--json");

        Assert.Equal(0, run.ExitCode);
        using var json = run.ReadJson();
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("succeeded").GetBoolean());
        Assert.Empty(data.GetProperty("pluginDiagnostics").EnumerateArray());
    }

    private static string CreatePluginPackage(string workingDirectory)
    {
        var packageRoot = Path.Combine(workingDirectory, "packages", "Sales.Plugin");
        Directory.CreateDirectory(Path.Combine(packageRoot, "atomui-city"));
        return packageRoot;
    }

    private static void WritePluginManifest(string packageRoot, string version = "1.0.0")
    {
        File.WriteAllText(
            Path.Combine(packageRoot, "atomui-city", "plugin.json"),
            $$"""
            {
              "schemaVersion": "1.0",
              "pluginId": "com.company.sales",
              "packageId": "Company.Sales.Plugin",
              "version": "{{version}}",
              "displayNameKey": "Sales.DisplayName",
              "mainAssembly": "Company.Sales.Plugin.dll",
              "targetFramework": "net10.0",
              "pluginApiVersion": "1.0",
              "minHostVersion": "1.0.0",
              "capabilities": [
                {
                  "name": "sales.read",
                  "scope": [ "tenant" ]
                }
              ]
            }
            """);
    }
}
