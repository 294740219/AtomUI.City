using System.Reflection;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.DependencyInjection;

internal sealed class GeneratedServiceRegistrationCatalog
{
    private readonly Dictionary<Type, OwnerRegistration> _registrations = [];
    private readonly HashSet<Type> _visitedRegistrars = [];
    private bool _frozen;

    private void ExecuteRegistrar(Type registrarType, Func<IServiceRegistrar> registrarFactory)
    {
        ArgumentNullException.ThrowIfNull(registrarType);
        ArgumentNullException.ThrowIfNull(registrarFactory);

        ThrowIfFrozen();

        if (!typeof(IServiceRegistrar).IsAssignableFrom(registrarType))
        {
            throw new ArgumentException(
                $"Generated service registrar '{registrarType.FullName}' must implement {nameof(IServiceRegistrar)}.",
                nameof(registrarType));
        }

        if (!registrarType.IsClass || registrarType.IsAbstract || registrarType.ContainsGenericParameters)
        {
            throw new ArgumentException(
                $"Generated service registrar '{registrarType.FullName}' must be a non-abstract, closed class.",
                nameof(registrarType));
        }

        if (!_visitedRegistrars.Add(registrarType))
        {
            return;
        }

        var registrar = registrarFactory()
            ?? throw new InvalidOperationException(
                $"Generated service registrar factory for '{registrarType.FullName}' returned null.");
        if (registrar.GetType() != registrarType)
        {
            throw new InvalidOperationException(
                $"Generated service registrar factory for '{registrarType.FullName}' returned " +
                $"'{registrar.GetType().FullName}'.");
        }

        var context = new RegistrarContext(this, registrarType);
        try
        {
            registrar.Register(context);
        }
        finally
        {
            context.Complete();
        }
    }

    internal void RegisterRegistrar(Type registrarType, Func<IServiceRegistrar> registrarFactory) =>
        ExecuteRegistrar(registrarType, registrarFactory);

    private void RegisterContribution(
        Type registrarType,
        Type ownerModuleType,
        Action<IServiceCollection> registration)
    {
        ArgumentNullException.ThrowIfNull(ownerModuleType);
        ArgumentNullException.ThrowIfNull(registration);

        ThrowIfFrozen();

        if (!typeof(IModule).IsAssignableFrom(ownerModuleType))
        {
            throw new ArgumentException(
                $"Service registration owner '{ownerModuleType.FullName}' must implement {nameof(IModule)}.",
                nameof(ownerModuleType));
        }

        if (ownerModuleType.Assembly != registrarType.Assembly)
        {
            throw new InvalidOperationException(
                $"Generated service registrar '{registrarType.FullName}' from assembly " +
                $"'{registrarType.Assembly.GetName().Name}' cannot register services for module owner " +
                $"'{ownerModuleType.FullName}' declared by assembly " +
                $"'{ownerModuleType.Assembly.GetName().Name}'. A registrar may only use an owner " +
                "declared by its own assembly; model cross-project composition with a local module and DependsOn.");
        }

        if (!ownerModuleType.IsClass || ownerModuleType.IsAbstract || ownerModuleType.ContainsGenericParameters)
        {
            throw new ArgumentException(
                $"Service registration owner '{ownerModuleType.FullName}' must be a non-abstract, closed class.",
                nameof(ownerModuleType));
        }

        if (!_registrations.TryGetValue(ownerModuleType, out var ownerRegistration))
        {
            ownerRegistration = new OwnerRegistration(registrarType);
            _registrations.Add(ownerModuleType, ownerRegistration);
        }
        else if (ownerRegistration.RegistrarType != registrarType)
        {
            throw new InvalidOperationException(
                $"Module owner '{ownerModuleType.FullName}' is claimed by multiple generated service registrars: " +
                $"'{ownerRegistration.RegistrarType.FullName}' from assembly " +
                $"'{ownerRegistration.RegistrarType.Assembly.GetName().Name}' and " +
                $"'{registrarType.FullName}' from assembly '{registrarType.Assembly.GetName().Name}'.");
        }

        ownerRegistration.Registrations.Add(registration);
    }

    public Action<IServiceCollection> Select(IReadOnlyList<ModuleRegistration> selectedModules)
    {
        ArgumentNullException.ThrowIfNull(selectedModules);
        _frozen = true;

        var registrations = OrderByDependencies(selectedModules)
            .Where(module => module.Descriptor?.Origin != ModuleOrigin.Plugin)
            .Where(module => _registrations.ContainsKey(module.ModuleType))
            .SelectMany(module => _registrations[module.ModuleType].Registrations)
            .ToArray();

        return services =>
        {
            ArgumentNullException.ThrowIfNull(services);
            foreach (var registration in registrations)
            {
                registration(services);
            }
        };
    }

    private static IReadOnlyList<ModuleRegistration> OrderByDependencies(
        IReadOnlyList<ModuleRegistration> modules)
    {
        var byType = modules.ToDictionary(module => module.ModuleType);
        var ordered = new List<ModuleRegistration>(modules.Count);
        var visited = new HashSet<Type>();
        var visiting = new HashSet<Type>();

        foreach (var module in modules.OrderBy(module => module.ModuleType.FullName, StringComparer.Ordinal))
        {
            Visit(module);
        }

        return ordered;

        void Visit(ModuleRegistration module)
        {
            if (visited.Contains(module.ModuleType))
            {
                return;
            }

            if (!visiting.Add(module.ModuleType))
            {
                throw new InvalidOperationException(
                    $"Circular module dependency detected at '{module.ModuleType.FullName}'.");
            }

            var descriptor = module.Descriptor ?? throw new InvalidOperationException(
                $"Module '{module.ModuleType.FullName}' does not have a descriptor.");
            foreach (var dependency in descriptor.Dependencies)
            {
                if (byType.TryGetValue(dependency.ModuleType, out var dependencyModule))
                {
                    Visit(dependencyModule);
                }
            }

            visiting.Remove(module.ModuleType);
            visited.Add(module.ModuleType);
            ordered.Add(module);
        }
    }

    public static GeneratedServiceRegistrationCatalog LoadGenerated(Assembly? applicationAssembly)
    {
        var catalog = new GeneratedServiceRegistrationCatalog();
        var manifest = applicationAssembly?.GetCustomAttribute<GeneratedServiceManifestAttribute>();

        if (manifest is null)
        {
            return catalog;
        }

        if (!typeof(IServiceRegistrar).IsAssignableFrom(manifest.RegistrarType))
        {
            throw new InvalidOperationException(
                $"Generated service registrar '{manifest.RegistrarType.FullName}' must implement {nameof(IServiceRegistrar)}.");
        }

        catalog.ExecuteRegistrar(
            manifest.RegistrarType,
            () => Activator.CreateInstance(manifest.RegistrarType) as IServiceRegistrar
                ?? throw new InvalidOperationException(
                    $"Generated service registrar '{manifest.RegistrarType.FullName}' could not be created."));
        return catalog;
    }

    private void ThrowIfFrozen()
    {
        if (_frozen)
        {
            throw new InvalidOperationException("Generated service registration catalog is frozen.");
        }
    }

    private sealed class OwnerRegistration(Type registrarType)
    {
        public Type RegistrarType { get; } = registrarType;

        public List<Action<IServiceCollection>> Registrations { get; } = [];
    }

    private sealed class RegistrarContext(
        GeneratedServiceRegistrationCatalog catalog,
        Type registrarType) : IServiceRegistrarContext
    {
        private int _completed;

        public void RegisterRegistrar(Type referencedRegistrarType, Func<IServiceRegistrar> registrarFactory)
        {
            ThrowIfCompleted();
            catalog.ExecuteRegistrar(referencedRegistrarType, registrarFactory);
        }

        public void Register(Type ownerModuleType, Action<IServiceCollection> registration)
        {
            ThrowIfCompleted();
            catalog.RegisterContribution(registrarType, ownerModuleType, registration);
        }

        internal void Complete() => Interlocked.Exchange(ref _completed, 1);

        private void ThrowIfCompleted()
        {
            if (Volatile.Read(ref _completed) != 0)
            {
                throw new InvalidOperationException(
                    $"Generated service registrar context for '{registrarType.FullName}' is no longer active.");
            }
        }
    }
}
