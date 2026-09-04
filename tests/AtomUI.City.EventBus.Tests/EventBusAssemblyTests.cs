using System.Reflection;
using AtomUI.City.Core.Modularity;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventBusAssemblyTests
{
    [Fact]
    public void EventBusAssemblyCanBeLoaded()
    {
        var assembly = Assembly.Load("AtomUI.City.EventBus");

        Assert.Equal("AtomUI.City.EventBus", assembly.GetName().Name);
    }

    [Fact]
    public void EventBusPackageAssemblyPublishesItsGeneratedModuleRegistrar()
    {
        var assembly = typeof(EventBusModule).Assembly;
        var manifest = assembly.GetCustomAttribute<GeneratedModuleManifestAttribute>();

        Assert.NotNull(manifest);
        Assert.True(manifest.RegistrarType.IsPublic);
        Assert.True(typeof(IModuleRegistrar).IsAssignableFrom(manifest.RegistrarType));

        var registrar = Assert.IsAssignableFrom<IModuleRegistrar>(Activator.CreateInstance(manifest.RegistrarType));
        var context = new RecordingModuleRegistrarContext();
        registrar.Register(context);

        var registration = Assert.Single(context.Registrations);
        Assert.Equal(typeof(EventBusModule), registration.Descriptor.ModuleType);
        Assert.IsType<EventBusModule>(registration.Factory());
        Assert.Empty(context.ApplicationRoots);
    }

    private sealed class RecordingModuleRegistrarContext : IModuleRegistrarContext
    {
        public List<(ModuleDescriptor Descriptor, Func<IModule> Factory)> Registrations { get; } = [];
        public List<Type> ApplicationRoots { get; } = [];

        public void Register(ModuleDescriptor descriptor, Func<IModule> factory)
        {
            Registrations.Add((descriptor, factory));
        }

        public void AddApplicationRoot(Type moduleType)
        {
            ApplicationRoots.Add(moduleType);
        }
    }
}
