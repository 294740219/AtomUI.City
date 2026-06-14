namespace AtomUI.City.PluginSystem;

public static class PluginMsBuildContract
{
    public static IReadOnlyList<string> Properties { get; } =
    [
        "AtomUICityPlugin",
        "AtomUICityPluginId",
        "AtomUICityPluginVersion",
        "AtomUICityPluginPublisher",
        "AtomUICityPluginDisplayNameKey",
        "AtomUICityPluginDescriptionKey",
        "AtomUICityMinHostVersion",
        "AtomUICityMaxHostVersion",
        "AtomUICityPluginApiVersion",
        "AtomUICityPluginUnloadable",
        "AtomUICityPluginNativeAotCompatible",
        "AtomUICityPluginResourceMode",
        "AtomUICityPluginGenerateManifest",
        "AtomUICityPluginValidateManifest",
        "AtomUICityPackageAsPlugin",
        "AtomUICityPluginDevelopmentMode",
    ];

    public static IReadOnlyList<string> Items { get; } =
    [
        "AtomUICityPluginCapability",
        "AtomUICityPluginDependency",
        "AtomUICityPluginContract",
        "AtomUICityLanguagePackage",
        "AtomUICityPluginAsset",
        "AtomUICityPluginNativeAsset",
        "AtomUICityContributionManifest",
    ];

    public static IReadOnlyList<string> Targets { get; } =
    [
        "GenerateAtomUICityPluginManifest",
        "GenerateAtomUICityContributionManifests",
        "ValidateAtomUICityPluginManifest",
        "ValidateAtomUICityPluginPackage",
        "PackAtomUICityPlugin",
        "InstallAtomUICityPluginToLocalCache",
        "CleanAtomUICityPluginArtifacts",
    ];

    public static IReadOnlyList<string> PackageContentRoots { get; } =
    [
        "lib/",
        PluginPackagePaths.ManifestRelativePath,
        "atomui-city/manifests/",
        "atomui-city/locales/",
        "atomui-city/assets/",
        "runtimes/",
    ];

    public static string GetManifestOutputPath(string intermediateOutputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intermediateOutputPath);

        return Path.Combine(
            Path.GetFullPath(intermediateOutputPath),
            "AtomUI.City",
            "plugin",
            "atomui-city",
            "plugin.json");
    }
}
