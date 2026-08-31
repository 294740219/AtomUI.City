using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Modularity;

public sealed class ModuleRegistry : IModuleRegistry, IAsyncDisposable
{
    private readonly IReadOnlyList<ModuleEntry> _orderedEntries;
    private readonly object _syncRoot = new();
    private bool _contributionsConfigured;
    private Task? _disposeTask;
    private bool _disposed;
    private bool _initialized;
    private bool _servicesConfigured;
    private Task? _shutdownTask;

    private ModuleRegistry(IReadOnlyList<ModuleEntry> orderedEntries)
    {
        _orderedEntries = orderedEntries;
        Modules = Array.AsReadOnly(orderedEntries.Select(entry => entry.Descriptor).ToArray());
    }

    public IReadOnlyList<ModuleDescriptor> Modules { get; }

    internal static ModuleRegistry Create(IReadOnlyList<ModuleRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var entries = new List<ModuleEntry>(registrations.Count);

        try
        {
            foreach (var registration in registrations)
            {
                entries.Add(new ModuleEntry(
                    CreateDescriptor(registration.ModuleType),
                    registration.Factory()));
            }

            return new ModuleRegistry(OrderByDependencies(entries));
        }
        catch
        {
            DisposeEntriesAfterBuildFailure(entries);
            throw;
        }
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
        ThrowIfDisposed();

        if (_servicesConfigured)
        {
            return;
        }

        var context = new ServiceConfigurationContext(applicationContext, services);
        var diagnostics = TryGetDiagnostics(services);

        try
        {
            await ExecuteConfigurationStageAsync(
                context,
                diagnostics,
                "PreConfigureServices",
                static (module, moduleContext, token) =>
                    module.PreConfigureServicesAsync(moduleContext, token),
                cancellationToken).ConfigureAwait(false);

            await ExecuteConfigurationStageAsync(
                context,
                diagnostics,
                "ConfigureServices",
                static (module, moduleContext, token) =>
                    module.ConfigureServicesAsync(moduleContext, token),
                cancellationToken).ConfigureAwait(false);

            await ExecuteConfigurationStageAsync(
                context,
                diagnostics,
                "PostConfigureServices",
                static (module, moduleContext, token) =>
                    module.PostConfigureServicesAsync(moduleContext, token),
                cancellationToken).ConfigureAwait(false);

            _servicesConfigured = true;
        }
        finally
        {
            context.Services.Freeze();
        }
    }

    public async ValueTask ConfigureContributionsAsync(
        ApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);
        ThrowIfDisposed();

        if (_contributionsConfigured)
        {
            return;
        }

        var context = new ContributionConfigurationContext(applicationContext, services);
        var diagnostics = services.GetService<IHostDiagnostics>();

        foreach (var entry in _orderedEntries)
        {
            entry.RuntimeEntered = true;
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
        ThrowIfDisposed();

        if (_initialized)
        {
            return;
        }

        var context = new ApplicationInitializationContext(applicationContext, services);
        var diagnostics = services.GetService<IHostDiagnostics>();

        await ExecuteInitializationStageAsync(
            context,
            diagnostics,
            "OnPreApplicationInitialization",
            static (module, moduleContext, token) =>
                module.OnPreApplicationInitializationAsync(moduleContext, token),
            cancellationToken).ConfigureAwait(false);

        await ExecuteInitializationStageAsync(
            context,
            diagnostics,
            "OnApplicationInitialization",
            static (module, moduleContext, token) =>
                module.OnApplicationInitializationAsync(moduleContext, token),
            cancellationToken).ConfigureAwait(false);

        await ExecuteInitializationStageAsync(
            context,
            diagnostics,
            "OnPostApplicationInitialization",
            static (module, moduleContext, token) =>
                module.OnPostApplicationInitializationAsync(moduleContext, token),
            cancellationToken).ConfigureAwait(false);

        _initialized = true;
    }

    public ValueTask ShutdownAsync(
        ApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.Shutdown);

        DeferredLifecycleOperation? operation = null;
        Task shutdownTask;

        lock (_syncRoot)
        {
            if (_shutdownTask is not null)
            {
                return new ValueTask(_shutdownTask);
            }

            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            operation = new DeferredLifecycleOperation();
            _shutdownTask = operation.Task;
            shutdownTask = _shutdownTask;
        }

        operation.Start(
            this,
            LifecycleOperationKind.Shutdown,
            () => ShutdownCoreAsync(applicationContext, services, cancellationToken));

        return new ValueTask(shutdownTask);
    }

    public ValueTask DisposeAsync()
    {
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.Dispose);

        DeferredLifecycleOperation? operation = null;
        Task disposeTask;

        lock (_syncRoot)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            operation = new DeferredLifecycleOperation();
            _disposeTask = operation.Task;
            disposeTask = _disposeTask;
        }

        operation.Start(
            this,
            LifecycleOperationKind.Dispose,
            () => DisposeModulesCoreAsync(diagnostics: null));

        return new ValueTask(disposeTask);
    }

    private async Task ShutdownCoreAsync(
        ApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();
        var context = new ApplicationShutdownContext(applicationContext, services);
        var diagnostics = services.GetService<IHostDiagnostics>();

        for (var index = _orderedEntries.Count - 1; index >= 0; index--)
        {
            var entry = _orderedEntries[index];

            if (!entry.RuntimeEntered || entry.ShutdownAttempted)
            {
                continue;
            }

            entry.ShutdownAttempted = true;

            try
            {
                await InvokeModuleAsync(
                    entry,
                    diagnostics,
                    "OnApplicationShutdown",
                    token => entry.Module.OnApplicationShutdownAsync(context, token),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AddFailure(failures, exception);
            }
        }

        try
        {
            await DisposeModulesCoreAsync(diagnostics).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        ThrowIfFailures(failures, "One or more modules failed to shut down.");
    }

    private async Task DisposeModulesCoreAsync(IHostDiagnostics? diagnostics)
    {
        var failures = new List<Exception>();

        for (var index = _orderedEntries.Count - 1; index >= 0; index--)
        {
            var entry = _orderedEntries[index];

            if (entry.Disposed)
            {
                continue;
            }

            entry.Disposed = true;

            try
            {
                switch (entry.Module)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch (Exception exception)
            {
                WriteModuleFailure(diagnostics, entry, "Dispose", exception);
                AddFailure(failures, exception);
            }
        }

        lock (_syncRoot)
        {
            _disposed = true;
        }

        ThrowIfFailures(failures, "One or more module instances failed to dispose.");
    }

    private async ValueTask ExecuteConfigurationStageAsync(
        ServiceConfigurationContext context,
        IHostDiagnostics? diagnostics,
        string stage,
        Func<IModule, ServiceConfigurationContext, CancellationToken, ValueTask> invoke,
        CancellationToken cancellationToken)
    {
        foreach (var entry in _orderedEntries)
        {
            await InvokeModuleAsync(
                entry,
                diagnostics,
                stage,
                token => invoke(entry.Module, context, token),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ExecuteInitializationStageAsync(
        ApplicationInitializationContext context,
        IHostDiagnostics? diagnostics,
        string stage,
        Func<IModule, ApplicationInitializationContext, CancellationToken, ValueTask> invoke,
        CancellationToken cancellationToken)
    {
        foreach (var entry in _orderedEntries)
        {
            entry.RuntimeEntered = true;
            await InvokeModuleAsync(
                entry,
                diagnostics,
                stage,
                token => invoke(entry.Module, context, token),
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
            cancellationToken.ThrowIfCancellationRequested();
            await invoke(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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

    private void ThrowIfDisposed()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ModuleRegistry));
            }
        }
    }

    private static void AddFailure(ICollection<Exception> failures, Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.Flatten().InnerExceptions)
            {
                failures.Add(innerException);
            }

            return;
        }

        failures.Add(exception);
    }

    private static void ThrowIfFailures(IReadOnlyCollection<Exception> failures, string message)
    {
        if (failures.Count > 0)
        {
            throw new AggregateException(message, failures);
        }
    }

    private static void DisposeEntriesAfterBuildFailure(IReadOnlyList<ModuleEntry> entries)
    {
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            try
            {
                switch (entries[index].Module)
                {
                    case IAsyncDisposable asyncDisposable:
                        asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch
            {
                // Preserve the module graph or construction failure.
            }
        }
    }

    private sealed class ModuleEntry(ModuleDescriptor descriptor, IModule module)
    {
        public ModuleDescriptor Descriptor { get; } = descriptor;

        public IModule Module { get; } = module;

        public bool RuntimeEntered { get; set; }

        public bool ShutdownAttempted { get; set; }

        public bool Disposed { get; set; }
    }

    private enum ModuleVisitState
    {
        Visiting,
        Visited,
    }
}
