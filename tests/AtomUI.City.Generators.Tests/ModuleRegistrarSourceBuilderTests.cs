using AtomUI.City.Generators.Modularity;

namespace AtomUI.City.Generators.Tests;

public sealed class ModuleRegistrarSourceBuilderTests
{
    [Fact]
    public void BuildCreatesAotFriendlyModuleRegistrar()
    {
        var modules = new[]
        {
            new ModuleMetadata(
                "Sample.Foundation",
                "Sample.App.FoundationModule",
                "1.0.0",
                "Foundation",
                []),
            new ModuleMetadata(
                "Sample.App",
                "Sample.App.AppModule",
                null,
                null,
                [new ModuleDependencyMetadata("Sample.App.FoundationModule", optional: false)],
                isApplicationRoot: true),
        };

        var source = ModuleRegistrarSourceBuilder.Build(
            "Sample.App",
            modules,
            ["Sample.Library.GeneratedModuleRegistrar"]);

        Assert.Contains("GeneratedModuleManifestAttribute", source, StringComparison.Ordinal);
        Assert.Contains("new global::Sample.Library.GeneratedModuleRegistrar().Register(context);", source, StringComparison.Ordinal);
        Assert.Contains("typeof(global::Sample.App.FoundationModule)", source, StringComparison.Ordinal);
        Assert.Contains("static () => new global::Sample.App.AppModule()", source, StringComparison.Ordinal);
        Assert.Contains("context.AddApplicationRoot(typeof(global::Sample.App.AppModule));", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Activator.CreateInstance", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrarTypeNameIsStableAndAssemblySpecific()
    {
        var first = ModuleRegistrarSourceBuilder.GetRegistrarTypeName("Sample.App");
        var second = ModuleRegistrarSourceBuilder.GetRegistrarTypeName("Sample-App");

        Assert.Equal(first, ModuleRegistrarSourceBuilder.GetRegistrarTypeName("Sample.App"));
        Assert.NotEqual(first, second);
    }
}
