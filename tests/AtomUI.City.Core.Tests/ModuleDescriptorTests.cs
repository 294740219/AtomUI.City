using AtomUI.City.Core.Modularity;

namespace AtomUI.City.Core.Tests;

public sealed class ModuleDescriptorTests
{
    [Fact]
    public void ModuleGraphFailureRecordsMissingDependencyPath()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModuleRegistry.CreateForTesting([typeof(DependsOnMissingModule)]));

        Assert.Contains(typeof(DependsOnMissingModule).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(MissingModule).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleGraphFailureRecordsCyclePath()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModuleRegistry.CreateForTesting([typeof(CycleStartModule), typeof(CycleEndModule)]));

        Assert.Contains(typeof(CycleStartModule).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(CycleEndModule).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleGraphFailureRecordsDuplicateModuleId()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ModuleRegistry.CreateForTesting([typeof(DuplicateModuleA), typeof(DuplicateModuleB)]));

        Assert.Contains("DuplicateModule", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DependenciesRejectExternalListMutation()
    {
        var sourceDependencies = new List<ModuleDependencyDescriptor>
        {
            new(typeof(DependencyModule), optional: false),
        };
        var descriptor = new ModuleDescriptor(
            "TestModule",
            typeof(TestModule),
            version: null,
            description: null,
            sourceDependencies);
        var dependencies = Assert.IsAssignableFrom<IList<ModuleDependencyDescriptor>>(descriptor.Dependencies);

        Assert.Throws<NotSupportedException>(() => dependencies[0] = new ModuleDependencyDescriptor(
            typeof(ReplacementModule),
            optional: true));
        Assert.Equal(typeof(DependencyModule), descriptor.Dependencies[0].ModuleType);
        Assert.False(descriptor.Dependencies[0].Optional);
    }

    [Fact]
    public void ModuleDescriptorRejectsNonModuleType()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ModuleDescriptor(
            "Invalid",
            typeof(string),
            version: null,
            description: null,
            []));

        Assert.Contains(nameof(IModule), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleDependencyDescriptorRejectsNonModuleType()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ModuleDependencyDescriptor(typeof(string), optional: false));

        Assert.Contains(nameof(IModule), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleDescriptorDefaultsToApplicationOrigin()
    {
        var descriptor = new ModuleDescriptor(
            "TestModule",
            typeof(TestModule),
            version: null,
            description: null,
            []);

        Assert.Equal(ModuleOrigin.Application, descriptor.Origin);
        Assert.Null(descriptor.PluginId);
    }

    [Fact]
    public void ModuleDescriptorCanDescribePluginOrigin()
    {
        var descriptor = new ModuleDescriptor(
            "PluginModule",
            typeof(TestModule),
            version: "1.0.0",
            description: "Plugin module",
            [],
            ModuleOrigin.Plugin,
            "sales-plugin");

        Assert.Equal(ModuleOrigin.Plugin, descriptor.Origin);
        Assert.Equal("sales-plugin", descriptor.PluginId);
    }

    [Fact]
    public void PluginModuleDescriptorRequiresPluginId()
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new ModuleDescriptor(
            "PluginModule",
            typeof(TestModule),
            version: null,
            description: null,
            [],
            ModuleOrigin.Plugin,
            pluginId: null));

        Assert.Contains("plugin", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplicationModuleDescriptorRejectsPluginId()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ModuleDescriptor(
            "ApplicationModule",
            typeof(TestModule),
            version: null,
            description: null,
            [],
            ModuleOrigin.Application,
            "sales-plugin"));

        Assert.Contains("plugin", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestModule : ModuleBase;

    private sealed class DependencyModule : ModuleBase;

    private sealed class ReplacementModule : ModuleBase;

    [DependsOn(typeof(MissingModule))]
    private sealed class DependsOnMissingModule : ModuleBase;

    private sealed class MissingModule : ModuleBase;

    [DependsOn(typeof(CycleEndModule))]
    private sealed class CycleStartModule : ModuleBase;

    [DependsOn(typeof(CycleStartModule))]
    private sealed class CycleEndModule : ModuleBase;

    [Module("DuplicateModule")]
    private sealed class DuplicateModuleA : ModuleBase;

    [Module("DuplicateModule")]
    private sealed class DuplicateModuleB : ModuleBase;
}
