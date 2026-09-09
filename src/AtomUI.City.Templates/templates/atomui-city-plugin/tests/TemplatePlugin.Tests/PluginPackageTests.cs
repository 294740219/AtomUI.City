using AtomUI.City.Core.Modularity;

namespace TemplatePlugin.Tests;

public sealed class PluginPackageTests
{
    [Fact]
    public void PluginModuleUsesCityModuleContract()
    {
        Assert.True(typeof(ModuleBase).IsAssignableFrom(typeof(TemplatePluginModule)));
    }
}
