using AtomUI.City.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Testing;

public sealed class ModuleTestHost : IDisposable, IAsyncDisposable
{
    private readonly TestHost _host;
    private readonly ServiceCollection _services = [];
    private ServiceProvider? _serviceProvider;
    private bool _disposed;
    private bool _initialized;
    private bool _shutdown;

    internal ModuleTestHost(TestHost host, IReadOnlyList<ModuleTestRecord> modules)
    {
        _host = host;
        Modules = Array.AsReadOnly(modules.ToArray());
    }

    public IReadOnlyList<ModuleTestRecord> Modules { get; }

    public TestHost Host => _host;

    public static ModuleTestHostBuilder CreateBuilder()
    {
        return new ModuleTestHostBuilder();
    }

    public async ValueTask InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        foreach (var module in Modules)
        {
            await InvokeModuleStageAsync(
                module,
                "PreConfigureServices",
                "AUCTEST301",
                () => module.Module.PreConfigureServicesAsync(CreateServiceConfigurationContext())).ConfigureAwait(false);
        }

        foreach (var module in Modules)
        {
            await InvokeModuleStageAsync(
                module,
                "ConfigureServices",
                "AUCTEST301",
                () => module.Module.ConfigureServicesAsync(CreateServiceConfigurationContext())).ConfigureAwait(false);
        }

        foreach (var module in Modules)
        {
            await InvokeModuleStageAsync(
                module,
                "PostConfigureServices",
                "AUCTEST301",
                () => module.Module.PostConfigureServicesAsync(CreateServiceConfigurationContext())).ConfigureAwait(false);
        }

        _serviceProvider = _services.BuildServiceProvider();

        foreach (var module in Modules)
        {
            await InvokeModuleStageAsync(
                module,
                "ConfigureContributions",
                "AUCTEST301",
                () => module.Module.ConfigureContributionsAsync(CreateContributionConfigurationContext())).ConfigureAwait(false);
        }

        foreach (var module in Modules)
        {
            await InvokeModuleStageAsync(
                module,
                "OnPreApplicationInitialization",
                "AUCTEST301",
                () => module.Module.OnPreApplicationInitializationAsync(CreateApplicationInitializationContext())).ConfigureAwait(false);
        }

        foreach (var module in Modules)
        {
            await InvokeModuleStageAsync(
                module,
                "OnApplicationInitialization",
                "AUCTEST301",
                () => module.Module.OnApplicationInitializationAsync(CreateApplicationInitializationContext())).ConfigureAwait(false);
        }

        foreach (var module in Modules)
        {
            await InvokeModuleStageAsync(
                module,
                "OnPostApplicationInitialization",
                "AUCTEST301",
                () => module.Module.OnPostApplicationInitializationAsync(CreateApplicationInitializationContext())).ConfigureAwait(false);
        }

        _initialized = true;
    }

    public async ValueTask ShutdownAsync()
    {
        if (_shutdown)
        {
            return;
        }

        _shutdown = true;

        if (_initialized)
        {
            for (var index = Modules.Count - 1; index >= 0; index--)
            {
                var module = Modules[index];
                await InvokeModuleStageAsync(
                    module,
                    "OnApplicationShutdown",
                    "AUCTEST302",
                    () => module.Module.OnApplicationShutdownAsync(CreateApplicationShutdownContext())).ConfigureAwait(false);
            }
        }

        await _host.StopAsync().ConfigureAwait(false);
        await DisposeServiceProviderAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ShutdownAsync().AsTask().GetAwaiter().GetResult();
        _host.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await ShutdownAsync().ConfigureAwait(false);
        await _host.DisposeAsync().ConfigureAwait(false);
    }

    private ServiceConfigurationContext CreateServiceConfigurationContext()
    {
        return new ServiceConfigurationContext(_host.ApplicationContext, _services);
    }

    private ContributionConfigurationContext CreateContributionConfigurationContext()
    {
        return new ContributionConfigurationContext(_host.ApplicationContext, GetServiceProvider());
    }

    private ApplicationInitializationContext CreateApplicationInitializationContext()
    {
        return new ApplicationInitializationContext(_host.ApplicationContext, GetServiceProvider());
    }

    private ApplicationShutdownContext CreateApplicationShutdownContext()
    {
        return new ApplicationShutdownContext(_host.ApplicationContext, GetServiceProvider());
    }

    private IServiceProvider GetServiceProvider()
    {
        return _serviceProvider ?? throw new InvalidOperationException("Module test host has not been initialized.");
    }

    private async ValueTask InvokeModuleStageAsync(
        ModuleTestRecord module,
        string stage,
        string diagnosticCode,
        Func<ValueTask> invoke)
    {
        try
        {
            await invoke().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _host.Diagnostics.Add(
                diagnosticCode,
                $"Module '{module.Name}' ({module.Module.GetType().FullName}) failed during {stage}: {exception.GetType().FullName}.");

            throw;
        }
    }

    private async ValueTask DisposeServiceProviderAsync()
    {
        if (_serviceProvider is null)
        {
            return;
        }

        await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        _serviceProvider = null;
    }
}
