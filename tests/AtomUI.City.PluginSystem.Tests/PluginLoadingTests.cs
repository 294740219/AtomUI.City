using AtomUI.City.PluginSystem;

namespace AtomUI.City.PluginSystem.Tests;

public sealed class PluginLoadingTests
{
    [Fact]
    public async Task LoaderLoadsMainAssemblyFromPluginRoot()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.WriteStandardManifest(mainAssembly: "AtomUI.City.PluginSystem.dll");
        workspace.CopyMainAssembly("AtomUI.City.PluginSystem.dll");
        var descriptor = PluginDescriptor.FromManifest(
            PluginManifestReader.Read(workspace.ManifestPath),
            workspace.Root);
        var loader = new PluginLoader();

        var result = await loader.LoadAsync(descriptor);

        Assert.True(result.Succeeded);
        Assert.Equal(PluginRuntimeState.Loaded, result.Runtime.State);
        Assert.Equal("AtomUI.City.PluginSystem", result.Runtime.MainAssembly.GetName().Name);
    }

    [Fact]
    public async Task RuntimeDeactivateAndUnloadUpdateState()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.WriteStandardManifest(mainAssembly: "AtomUI.City.PluginSystem.dll");
        workspace.CopyMainAssembly("AtomUI.City.PluginSystem.dll");
        var descriptor = PluginDescriptor.FromManifest(
            PluginManifestReader.Read(workspace.ManifestPath),
            workspace.Root);
        var loader = new PluginLoader();
        var result = await loader.LoadAsync(descriptor);

        result.Runtime.Activate();
        await result.Runtime.DeactivateAsync();
        await result.Runtime.UnloadAsync();

        Assert.Equal(PluginRuntimeState.Unloaded, result.Runtime.State);
    }

    [Fact]
    public async Task LoaderRejectsMissingMainAssembly()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.WriteStandardManifest(mainAssembly: "Missing.Plugin.dll");
        var descriptor = PluginDescriptor.FromManifest(
            PluginManifestReader.Read(workspace.ManifestPath),
            workspace.Root);
        var loader = new PluginLoader();

        var result = await loader.LoadAsync(descriptor);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == PluginDiagnosticIds.MainAssemblyNotFound);
    }

    [Fact]
    public async Task DiscoveryScannerReportsInvalidPluginDirectoryEntriesAndContinues()
    {
        using var workspace = new PluginTestWorkspace();
        workspace.WriteStandardManifest();
        workspace.CopyMainAssembly("Company.Sales.Plugin.dll");
        var pluginsRoot = workspace.CreateDirectory("plugins");
        var installer = new PluginPackageInstaller();
        await installer.InstallFromDirectoryAsync(workspace.Root, pluginsRoot);
        var invalidPluginDirectory = Path.Combine(
            pluginsRoot,
            PluginPackagePaths.InstalledDirectoryName,
            "com.company.broken");
        await File.WriteAllTextAsync(invalidPluginDirectory, "not a directory");

        var discovery = PluginDiscoveryScanner.DiscoverInstalled(pluginsRoot);

        Assert.False(discovery.Succeeded);
        Assert.Single(discovery.Plugins);
        Assert.Contains(
            discovery.Diagnostics,
            diagnostic => diagnostic.Code == PluginDiagnosticIds.InvalidPluginDirectory
                && diagnostic.PluginId == "com.company.broken"
                && diagnostic.Field == "pluginDirectory"
                && diagnostic.Path == invalidPluginDirectory);
    }
}
