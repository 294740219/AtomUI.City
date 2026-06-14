using AtomUI.City.PluginSystem;

namespace AtomUI.City.PluginSystem.Tests;

public sealed class PluginResultTests
{
    [Fact]
    public void DiagnosticIdsExposeStableUniqueCatalog()
    {
        Assert.Equal(
            Enumerable.Range(0, 24).Select(index => $"AUCPLG{index:0000}"),
            PluginDiagnosticIds.All);
        Assert.Equal(
            PluginDiagnosticIds.All.Count,
            PluginDiagnosticIds.All.Distinct(StringComparer.Ordinal).Count());
        var mutable = Assert.IsAssignableFrom<IList<string>>(PluginDiagnosticIds.All);
        Assert.Throws<NotSupportedException>(() => mutable[0] = "AUCPLG9999");
    }

    [Fact]
    public void DiagnosticRequiresCodeAndMessage()
    {
        Assert.Throws<ArgumentException>(() => new PluginDiagnostic(string.Empty, "failure"));
        Assert.Throws<ArgumentException>(() => new PluginDiagnostic("AUCPLGTEST", string.Empty));
    }

    [Fact]
    public void LoadResultDiagnosticsRejectExternalListMutation()
    {
        var diagnostic = new PluginDiagnostic("AUCPLGTEST", "failure");
        var replacement = new PluginDiagnostic("AUCPLGOTHER", "replacement");
        var result = PluginLoadResult.Failed([diagnostic]);
        var diagnostics = Assert.IsAssignableFrom<IList<PluginDiagnostic>>(result.Diagnostics);

        Assert.Throws<NotSupportedException>(() => diagnostics[0] = replacement);
        Assert.Equal(diagnostic.Code, result.Diagnostics[0].Code);
    }

    [Fact]
    public void InstallResultDiagnosticsRejectExternalListMutation()
    {
        var diagnostic = new PluginDiagnostic("AUCPLGTEST", "failure");
        var replacement = new PluginDiagnostic("AUCPLGOTHER", "replacement");
        var result = PluginInstallResult.Failed([diagnostic]);
        var diagnostics = Assert.IsAssignableFrom<IList<PluginDiagnostic>>(result.Diagnostics);

        Assert.Throws<NotSupportedException>(() => diagnostics[0] = replacement);
        Assert.Equal(diagnostic.Code, result.Diagnostics[0].Code);
    }

    [Fact]
    public void ValidationResultDiagnosticsRejectExternalListMutation()
    {
        var diagnostic = new PluginDiagnostic("AUCPLGTEST", "failure");
        var replacement = new PluginDiagnostic("AUCPLGOTHER", "replacement");
        var result = new PluginValidationResult([diagnostic]);
        var diagnostics = Assert.IsAssignableFrom<IList<PluginDiagnostic>>(result.Diagnostics);

        Assert.Throws<NotSupportedException>(() => diagnostics[0] = replacement);
        Assert.Equal(diagnostic.Code, result.Diagnostics[0].Code);
    }
}
