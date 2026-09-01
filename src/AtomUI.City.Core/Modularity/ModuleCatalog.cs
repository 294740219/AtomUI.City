using System.Reflection;

namespace AtomUI.City.Core.Modularity;

internal sealed class ModuleCatalog : IModuleRegistrarContext
{
    private readonly List<Type> _applicationRoots = [];
    private readonly Dictionary<string, Type> _moduleTypesByName = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, ModuleRegistration> _registrations = [];
    private bool _frozen;

    public void Register(ModuleDescriptor descriptor, Func<IModule> factory)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(factory);
        ThrowIfFrozen();

        if (_registrations.TryGetValue(descriptor.ModuleType, out var existing))
        {
            EnsureEquivalent(
                existing.Descriptor ?? throw new InvalidOperationException(
                    $"Catalog module '{descriptor.ModuleType.FullName}' does not have a descriptor."),
                descriptor);
            return;
        }

        if (_moduleTypesByName.TryGetValue(descriptor.Name, out var existingType) &&
            existingType != descriptor.ModuleType)
        {
            throw new InvalidOperationException(
                $"Duplicate module id '{descriptor.Name}' declared by '{existingType.FullName}' and '{descriptor.ModuleType.FullName}'.");
        }

        _registrations.Add(
            descriptor.ModuleType,
            new ModuleRegistration(descriptor, factory));
        _moduleTypesByName.Add(descriptor.Name, descriptor.ModuleType);
    }

    public void AddApplicationRoot(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        ThrowIfFrozen();

        if (!typeof(IModule).IsAssignableFrom(moduleType))
        {
            throw new ArgumentException(
                $"Application root type '{moduleType.FullName}' must implement {nameof(IModule)}.",
                nameof(moduleType));
        }

        if (!_applicationRoots.Contains(moduleType))
        {
            _applicationRoots.Add(moduleType);
        }
    }

    public IReadOnlyList<ModuleRegistration> Resolve(
        IReadOnlyList<ModuleRegistration> explicitRoots)
    {
        ArgumentNullException.ThrowIfNull(explicitRoots);
        ThrowIfFrozen();

        var roots = new List<Type>(_applicationRoots);

        foreach (var explicitRoot in explicitRoots)
        {
            if (!_registrations.ContainsKey(explicitRoot.ModuleType))
            {
                Register(
                    ModuleDescriptorFactory.CreateFromAttributes(explicitRoot.ModuleType),
                    explicitRoot.Factory);
            }

            if (!roots.Contains(explicitRoot.ModuleType))
            {
                roots.Add(explicitRoot.ModuleType);
            }
        }

        _frozen = true;
        var selected = new HashSet<Type>();

        foreach (var root in roots)
        {
            SelectRequiredClosure(root, selected);
        }

        return Array.AsReadOnly(
            _registrations
                .Where(pair => selected.Contains(pair.Key))
                .Select(pair => pair.Value)
                .ToArray());
    }

    public static ModuleCatalog LoadGenerated(Assembly? applicationAssembly)
    {
        var catalog = new ModuleCatalog();

        if (applicationAssembly is null)
        {
            return catalog;
        }

        var manifest = applicationAssembly.GetCustomAttribute<GeneratedModuleManifestAttribute>();

        if (manifest is null)
        {
            return catalog;
        }

        if (!typeof(IModuleRegistrar).IsAssignableFrom(manifest.RegistrarType))
        {
            throw new InvalidOperationException(
                $"Generated module registrar '{manifest.RegistrarType.FullName}' must implement {nameof(IModuleRegistrar)}.");
        }

        var registrar = Activator.CreateInstance(manifest.RegistrarType) as IModuleRegistrar
            ?? throw new InvalidOperationException(
                $"Generated module registrar '{manifest.RegistrarType.FullName}' could not be created.");

        registrar.Register(catalog);
        return catalog;
    }

    private void SelectRequiredClosure(Type moduleType, ISet<Type> selected)
    {
        if (selected.Contains(moduleType))
        {
            return;
        }

        if (!_registrations.TryGetValue(moduleType, out var registration))
        {
            throw new InvalidOperationException(
                $"Startup module '{moduleType.FullName}' is not present in the module catalog.");
        }

        selected.Add(moduleType);

        var descriptor = registration.Descriptor
            ?? throw new InvalidOperationException(
                $"Catalog module '{moduleType.FullName}' does not have a descriptor.");

        foreach (var dependency in descriptor.Dependencies)
        {
            if (!_registrations.ContainsKey(dependency.ModuleType))
            {
                if (dependency.Optional)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Module '{descriptor.Name}' depends on missing module '{dependency.ModuleType.FullName}'.");
            }

            SelectRequiredClosure(dependency.ModuleType, selected);
        }
    }

    private static void EnsureEquivalent(ModuleDescriptor existing, ModuleDescriptor incoming)
    {
        var dependenciesMatch = existing.Dependencies.Count == incoming.Dependencies.Count &&
            existing.Dependencies.Zip(incoming.Dependencies).All(pair =>
                pair.First.ModuleType == pair.Second.ModuleType &&
                pair.First.Optional == pair.Second.Optional);

        if (!string.Equals(existing.Name, incoming.Name, StringComparison.Ordinal) ||
            !string.Equals(existing.Version, incoming.Version, StringComparison.Ordinal) ||
            !string.Equals(existing.Description, incoming.Description, StringComparison.Ordinal) ||
            existing.Origin != incoming.Origin ||
            !string.Equals(existing.PluginId, incoming.PluginId, StringComparison.Ordinal) ||
            !dependenciesMatch)
        {
            throw new InvalidOperationException(
                $"Module '{incoming.ModuleType.FullName}' was registered with conflicting descriptors.");
        }
    }

    private void ThrowIfFrozen()
    {
        if (_frozen)
        {
            throw new InvalidOperationException("Module catalog is frozen.");
        }
    }
}
