using AtomUI.City.Diagnostics;
using AtomUI.City.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Modularity;

public sealed class ModuleRegistry : IModuleRegistry
{
    private readonly IReadOnlyList<ModuleEntry> _orderedEntries;
    private bool _contributionsConfigured;
    private bool _initialized;
    private bool _servicesConfigured;
    private bool _shutdown;

    private ModuleRegistry(IReadOnlyList<ModuleEntry> orderedEntries)
    {
        _orderedEntries = orderedEntries;
        Modules = Array.AsReadOnly(orderedEntries.Select(entry => entry.Descriptor).ToArray());
    }

    public IReadOnlyList<ModuleDescriptor> Modules { get; }

    internal static ModuleRegistry Create(IReadOnlyList<ModuleRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var entries = registrations
            .Select(registration => new ModuleEntry(
                CreateDescriptor(registration.ModuleType),
                registration.Factory()))
            .ToArray();

        return new ModuleRegistry(OrderByDependencies(entries));
    }

    internal static ModuleRegistry CreateForTesting(IReadOnlyList<Type> moduleTypes)
    {
        ArgumentNullException.ThrowIfNull(moduleTypes);

        var registrations = moduleTypes
            .Select(moduleType => new ModuleRegistration(
                moduleType,
                () => (IModule)Activator.CreateInstance(moduleType)!))
            .ToArray();

        return Create(registrations);
    }

    public async ValueTask ConfigureServicesAsync(
        ApplicationContext applicationContext,
        IServiceCollection services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);

        if (_servicesConfigured)
        {
            return;
        }

        var context = new ServiceConfigurationContext(applicationContext, services);
        var diagnostics = TryGetDiagnostics(services);

        foreach (var entry in _orderedEntries)
        {
            await InvokeModuleAsync(
                entry,
                diagnostics,
                "PreConfigureServices",
                token => entry.Module.PreConfigureServicesAsync(context, token),
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var entry in _orderedEntries)
        {
            await InvokeModuleAsync(
                entry,
                diagnostics,
                "ConfigureServices",
                token => entry.Module.ConfigureServicesAsync(context, token),
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var entry in _orderedEntries)
        {
            await InvokeModuleAsync(
                entry,
                diagnostics,
                "PostConfigureServices",
                token => entry.Module.PostConfigureServicesAsync(context, token),
                cancellationToken).ConfigureAwait(false);
        }

        _servicesConfigured = true;
    }

    public async ValueTask ConfigureContributionsAsync(
        ApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);

        if (_contributionsConfigured)
        {
            return;
        }

        var context = new ContributionConfigurationContext(applicationContext, services);
        var diagnostics = services.GetService<IHostDiagnostics>();

        foreach (var entry in _orderedEntries)
        {
            await InvokeModuleAsync(
                entry,
                diagnostics,
                "ConfigureContributions",
                token => entry.Module.ConfigureContributionsAsync(context, token),
                cancellationToken).ConfigureAwait(false);
        }

        _contributionsConfigured = true;
    }

    public async ValueTask InitializeAsync(
        ApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);

        if (_initialized)
        {
            return;
        }

        var context = new ApplicationInitializationContext(applicationContext, services);
        var diagnostics = services.GetService<IHostDiagnostics>();

        foreach (var entry in _orderedEntries)
        {
            await InvokeModuleAsync(
                entry,
                diagnostics,
                "OnPreApplicationInitialization",
                token => entry.Module.OnPreApplicationInitializationAsync(context, token),
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var entry in _orderedEntries)
        {
            await InvokeModuleAsync(
                entry,
                diagnostics,
                "OnApplicationInitialization",
                token => entry.Module.OnApplicationInitializationAsync(context, token),
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var entry in _orderedEntries)
        {
            await InvokeModuleAsync(
                entry,
                diagnostics,
                "OnPostApplicationInitialization",
                token => entry.Module.OnPostApplicationInitializationAsync(context, token),
                cancellationToken).ConfigureAwait(false);
        }

        _initialized = true;
    }

    public async ValueTask ShutdownAsync(
        ApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);

        if (_shutdown || !_initialized)
        {
            return;
        }

        _shutdown = true;
        var context = new ApplicationShutdownContext(applicationContext, services);
        var diagnostics = services.GetService<IHostDiagnostics>();

        for (var index = _orderedEntries.Count - 1; index >= 0; index--)
        {
            var entry = _orderedEntries[index];
            await InvokeModuleAsync(
                entry,
                diagnostics,
                "OnApplicationShutdown",
                token => entry.Module.OnApplicationShutdownAsync(context, token),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static ModuleDescriptor CreateDescriptor(Type moduleType)
    {
        var attribute = moduleType
            .GetCustomAttributes(typeof(ModuleAttribute), inherit: false)
            .OfType<ModuleAttribute>()
            .SingleOrDefault();
        var dependencies = moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .OfType<DependsOnAttribute>()
            .Select(attribute => new ModuleDependencyDescriptor(attribute.ModuleType, attribute.Optional))
            .ToArray();

        return new ModuleDescriptor(
            attribute?.Name ?? moduleType.FullName ?? moduleType.Name,
            moduleType,
            attribute?.Version,
            attribute?.Description,
            dependencies);
    }

    private static IReadOnlyList<ModuleEntry> OrderByDependencies(IReadOnlyList<ModuleEntry> entries)
    {
        var entriesByType = entries.ToDictionary(entry => entry.Descriptor.ModuleType);
        var duplicateId = entries
            .GroupBy(entry => entry.Descriptor.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateId is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate module id '{duplicateId.Key}' declared by {string.Join(", ", duplicateId.Select(entry => entry.Descriptor.ModuleType.FullName))}.");
        }

        var ordered = new List<ModuleEntry>();
        var visitStates = new Dictionary<Type, ModuleVisitState>();
        var path = new Stack<Type>();

        foreach (var entry in entries)
        {
            Visit(entry, entriesByType, visitStates, ordered, path);
        }

        return ordered;
    }

    private static void Visit(
        ModuleEntry entry,
        IReadOnlyDictionary<Type, ModuleEntry> entriesByType,
        IDictionary<Type, ModuleVisitState> visitStates,
        ICollection<ModuleEntry> ordered,
        Stack<Type> path)
    {
        if (visitStates.TryGetValue(entry.Descriptor.ModuleType, out var state))
        {
            if (state == ModuleVisitState.Visited)
            {
                return;
            }

            var cyclePath = path
                .Reverse()
                .SkipWhile(type => type != entry.Descriptor.ModuleType)
                .Append(entry.Descriptor.ModuleType)
                .Select(type => type.FullName);

            throw new InvalidOperationException(
                $"Module dependency graph contains a cycle: {string.Join(" -> ", cyclePath)}.");
        }

        visitStates.Add(entry.Descriptor.ModuleType, ModuleVisitState.Visiting);
        path.Push(entry.Descriptor.ModuleType);

        foreach (var dependency in entry.Descriptor.Dependencies)
        {
            if (entriesByType.TryGetValue(dependency.ModuleType, out var dependencyEntry))
            {
                Visit(dependencyEntry, entriesByType, visitStates, ordered, path);
                continue;
            }

            if (!dependency.Optional)
            {
                throw new InvalidOperationException(
                    $"Module '{entry.Descriptor.ModuleType.FullName}' depends on missing module '{dependency.ModuleType.FullName}'.");
            }
        }

        visitStates[entry.Descriptor.ModuleType] = ModuleVisitState.Visited;
        path.Pop();
        ordered.Add(entry);
    }

    private static IHostDiagnostics? TryGetDiagnostics(IServiceCollection services)
    {
        return services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(IHostDiagnostics))
            ?.ImplementationInstance as IHostDiagnostics;
    }

    private static async ValueTask InvokeModuleAsync(
        ModuleEntry entry,
        IHostDiagnostics? diagnostics,
        string stage,
        Func<CancellationToken, ValueTask> invoke,
        CancellationToken cancellationToken)
    {
        try
        {
            await invoke(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WriteModuleFailure(diagnostics, entry, stage, exception);

            throw;
        }
    }

    private static void WriteModuleFailure(
        IHostDiagnostics? diagnostics,
        ModuleEntry entry,
        string stage,
        Exception exception)
    {
        if (diagnostics is null)
        {
            return;
        }

        try
        {
            diagnostics.Write(new HostDiagnosticRecord(
                HostDiagnosticIds.ModuleLifecycleFailed,
                "Module lifecycle stage failed.",
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>
                {
                    ["moduleId"] = entry.Descriptor.Name,
                    ["moduleType"] = entry.Descriptor.ModuleType.FullName,
                    ["stage"] = stage,
                    ["exceptionType"] = exception.GetType().FullName,
                },
            });
        }
        catch
        {
            // Diagnostics must not replace the original module failure.
        }
    }

    private sealed record ModuleEntry(ModuleDescriptor Descriptor, IModule Module);

    private enum ModuleVisitState
    {
        Visiting,
        Visited,
    }
}
