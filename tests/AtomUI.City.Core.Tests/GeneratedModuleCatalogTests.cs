using AtomUI.City.Core.Modularity;

namespace AtomUI.City.Core.Tests;

public sealed class GeneratedModuleCatalogTests
{
    [Fact]
    public void GeneratedApplicationRootResolvesOnlyRequiredClosure()
    {
        var catalog = new ModuleCatalog();
        catalog.Register(Descriptor<FoundationModule>(), static () => new FoundationModule());
        catalog.Register(
            Descriptor<AppModule>(new ModuleDependencyDescriptor(typeof(FoundationModule), optional: false)),
            static () => new AppModule());
        catalog.Register(Descriptor<UnusedModule>(), static () => new UnusedModule());
        catalog.AddApplicationRoot(typeof(AppModule));

        var resolved = catalog.Resolve([]);

        Assert.Equal(
            [typeof(AppModule), typeof(FoundationModule)],
            resolved.Select(registration => registration.ModuleType).OrderBy(type => type.Name));
        Assert.DoesNotContain(resolved, registration => registration.ModuleType == typeof(UnusedModule));
    }

    [Fact]
    public void ExplicitAndGeneratedRootAreDeduplicated()
    {
        var catalog = new ModuleCatalog();
        catalog.Register(Descriptor<AppModule>(), static () => new AppModule());
        catalog.AddApplicationRoot(typeof(AppModule));

        var resolved = catalog.Resolve(
            [new ModuleRegistration(typeof(AppModule), static () => new AppModule())]);

        Assert.Single(resolved);
        Assert.Equal(typeof(AppModule), resolved[0].ModuleType);
        Assert.NotNull(resolved[0].Descriptor);
    }

    [Fact]
    public void ExplicitAndGeneratedRootsAreMerged()
    {
        var catalog = new ModuleCatalog();
        catalog.Register(Descriptor<AppModule>(), static () => new AppModule());
        catalog.Register(Descriptor<FoundationModule>(), static () => new FoundationModule());
        catalog.AddApplicationRoot(typeof(AppModule));

        var resolved = catalog.Resolve(
            [new ModuleRegistration(typeof(FoundationModule), static () => new FoundationModule())]);

        Assert.Equal(
            [typeof(AppModule), typeof(FoundationModule)],
            resolved.Select(registration => registration.ModuleType).OrderBy(type => type.Name));
    }

    [Fact]
    public void AvailableOptionalDependencyIsSelected()
    {
        var catalog = new ModuleCatalog();
        catalog.Register(Descriptor<FoundationModule>(), static () => new FoundationModule());
        catalog.Register(
            Descriptor<AppModule>(new ModuleDependencyDescriptor(typeof(FoundationModule), optional: true)),
            static () => new AppModule());
        catalog.AddApplicationRoot(typeof(AppModule));

        var resolved = catalog.Resolve([]);

        Assert.Contains(resolved, registration => registration.ModuleType == typeof(FoundationModule));
    }

    [Fact]
    public void MissingOptionalDependencyIsIgnored()
    {
        var catalog = new ModuleCatalog();
        catalog.Register(
            Descriptor<AppModule>(new ModuleDependencyDescriptor(typeof(FoundationModule), optional: true)),
            static () => new AppModule());
        catalog.AddApplicationRoot(typeof(AppModule));

        var resolved = catalog.Resolve([]);

        Assert.Single(resolved);
        Assert.Equal(typeof(AppModule), resolved[0].ModuleType);
    }

    [Fact]
    public void ExplicitRootUsesCompatibilityDescriptorWhenCatalogIsMissing()
    {
        var catalog = new ModuleCatalog();

        var resolved = catalog.Resolve(
            [new ModuleRegistration(typeof(AppModule), static () => new AppModule())]);

        var registration = Assert.Single(resolved);
        Assert.Equal(typeof(AppModule).FullName, registration.Descriptor?.Name);
    }

    [Fact]
    public void RequiredDependencyMissingFromCatalogFails()
    {
        var catalog = new ModuleCatalog();
        catalog.Register(
            Descriptor<AppModule>(new ModuleDependencyDescriptor(typeof(FoundationModule), optional: false)),
            static () => new AppModule());
        catalog.AddApplicationRoot(typeof(AppModule));

        var exception = Assert.Throws<InvalidOperationException>(() => catalog.Resolve([]));

        Assert.Contains(typeof(FoundationModule).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConflictingGeneratedDescriptorsFail()
    {
        var catalog = new ModuleCatalog();
        catalog.Register(Descriptor<AppModule>(), static () => new AppModule());

        var exception = Assert.Throws<InvalidOperationException>(() => catalog.Register(
            new ModuleDescriptor("Changed", typeof(AppModule), null, null, []),
            static () => new AppModule()));

        Assert.Contains("conflicting descriptors", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveFreezesCatalogAndApplicationRoots()
    {
        var catalog = new ModuleCatalog();
        catalog.Register(Descriptor<AppModule>(), static () => new AppModule());
        catalog.AddApplicationRoot(typeof(AppModule));

        _ = catalog.Resolve([]);

        Assert.Throws<InvalidOperationException>(() => catalog.Register(
            Descriptor<UnusedModule>(),
            static () => new UnusedModule()));
        Assert.Throws<InvalidOperationException>(() => catalog.AddApplicationRoot(typeof(UnusedModule)));
        Assert.Throws<InvalidOperationException>(() => catalog.Resolve([]));
    }

    private static ModuleDescriptor Descriptor<TModule>(
        params ModuleDependencyDescriptor[] dependencies)
        where TModule : IModule
    {
        return new ModuleDescriptor(
            typeof(TModule).FullName!,
            typeof(TModule),
            null,
            null,
            dependencies);
    }

    private sealed class FoundationModule : ModuleBase;

    private sealed class AppModule : ModuleBase;

    private sealed class UnusedModule : ModuleBase;
}
