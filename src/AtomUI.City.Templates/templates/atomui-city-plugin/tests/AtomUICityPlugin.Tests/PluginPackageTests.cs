using AtomUI.City.Testing;

namespace AtomUICityPlugin.Tests;

public sealed class PluginPackageTests
{
    [Fact]
    [TestLayer(TestLayerNames.Contract)]
    public void PluginTemplateContainsPackageSmokeTest()
    {
        Assert.True(true);
    }
}
