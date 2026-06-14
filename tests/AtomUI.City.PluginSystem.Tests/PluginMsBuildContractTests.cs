using AtomUI.City.PluginSystem;

namespace AtomUI.City.PluginSystem.Tests;

public sealed class PluginMsBuildContractTests
{
    [Fact]
    public void PluginMsBuildContractContainsDocumentedPropertiesItemsAndTargets()
    {
        Assert.Contains("AtomUICityPlugin", PluginMsBuildContract.Properties);
        Assert.Contains("AtomUICityPluginId", PluginMsBuildContract.Properties);
        Assert.Contains("AtomUICityPackageAsPlugin", PluginMsBuildContract.Properties);
        Assert.Contains("AtomUICityPluginGenerateManifest", PluginMsBuildContract.Properties);
        Assert.Contains("AtomUICityPluginValidateManifest", PluginMsBuildContract.Properties);
        Assert.Contains("AtomUICityPluginDevelopmentMode", PluginMsBuildContract.Properties);
        Assert.Contains("AtomUICityPluginCapability", PluginMsBuildContract.Items);
        Assert.Contains("AtomUICityPluginDependency", PluginMsBuildContract.Items);
        Assert.Contains("GenerateAtomUICityPluginManifest", PluginMsBuildContract.Targets);
        Assert.Contains("ValidateAtomUICityPluginPackage", PluginMsBuildContract.Targets);
        Assert.Contains("PackAtomUICityPlugin", PluginMsBuildContract.Targets);
        Assert.Contains("InstallAtomUICityPluginToLocalCache", PluginMsBuildContract.Targets);
    }

    [Fact]
    public void PluginMsBuildContractDefinesManifestOutputPath()
    {
        var intermediateOutputPath = Path.Combine(Path.GetTempPath(), "atomui-city", "obj", "..", "obj");
        var outputPath = PluginMsBuildContract.GetManifestOutputPath(intermediateOutputPath);

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(intermediateOutputPath),
                "AtomUI.City",
                "plugin",
                "atomui-city",
                "plugin.json"),
            outputPath);
    }

    [Fact]
    public void PluginMsBuildContractDefinesPackageContentRoots()
    {
        Assert.Contains("lib/", PluginMsBuildContract.PackageContentRoots);
        Assert.Contains("atomui-city/plugin.json", PluginMsBuildContract.PackageContentRoots);
        Assert.Contains("atomui-city/manifests/", PluginMsBuildContract.PackageContentRoots);
        Assert.Contains("runtimes/", PluginMsBuildContract.PackageContentRoots);
    }
}
