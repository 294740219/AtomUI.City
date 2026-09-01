using System.Collections;
using System.Reflection;
using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Core.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace AtomUI.City.Core.Hosting;

public sealed class ApplicationHostBuilder : IApplicationHostBuilder
{
    private readonly string[] _args;
    private readonly HostApplicationBuilder _builder;
    private readonly List<Action<ApplicationHostOptions>> _configureHostActions = [];
    private readonly List<Action<IServiceCollection>> _configureServicesActions = [];
    private readonly GuardedConfigurationManager _configuration;
    private readonly GuardedServiceCollection _services;
    private bool _applyingUserServices;
    private bool _built;

    internal ApplicationHostBuilder(string[] args)
    {
        _args = args.ToArray();
        _builder = Host.CreateApplicationBuilder(_args);

        if (OperatingSystem.IsWindows())
        {
            _builder.Logging.AddFilter<EventLogLoggerProvider>(static _ => false);
        }

        _configuration = new GuardedConfigurationManager(_builder.Configuration, ThrowIfBuilt);
        _services = new GuardedServiceCollection(
            _builder.Services,
            () => _applyingUserServices);
    }

    public IConfigurationManager Configuration => _configuration;

    public IApplicationHostBuilder ConfigureServices(Action<IServiceCollection> configureServices)
    {
        ArgumentNullException.ThrowIfNull(configureServices);
        ThrowIfBuilt();

        _configureServicesActions.Add(configureServices);

        return this;
    }

    public IApplicationHostBuilder ConfigureHost(Action<ApplicationHostOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);
        ThrowIfBuilt();

        _configureHostActions.Add(configureOptions);

        return this;
    }

    public IApplicationHost Build()
    {
        if (_built)
        {
            throw new InvalidOperationException("Application host builder can only build once.");
        }

        var buildDiagnostics = ApplicationHostBuilderDiagnosticsStore.GetOrCreate(this);
        _built = true;
        var moduleRegistrations = ModuleRegistrationStore.FreezeAndSnapshot(this);
        var lifecycleConfigurations = LifecycleConfigurationStore.FreezeAndSnapshot(this);
        ModuleRegistry? moduleRegistry = null;
        IHost? genericHost = null;
        IHostDiagnostics? runtimeDiagnostics = null;
        var buildStage = "Options";

        try
        {
            var hostOptions = CreateHostOptions();
            ValidateHostOptions(hostOptions);
            var context = CreateApplicationContext(hostOptions);
            var lifecyclePipeline = LifecycleConfigurationStore.Build(lifecycleConfigurations);

            buildStage = "ModuleGraph";
            var moduleCatalog = ModuleCatalog.LoadGenerated(Assembly.GetEntryAssembly());
            var resolvedModuleRegistrations = moduleCatalog.Resolve(moduleRegistrations);

            moduleRegistry = ModuleRegistry.Create(resolvedModuleRegistrations);
            runtimeDiagnostics = GetRegisteredDiagnosticsInstance()
                ?? new InMemoryHostDiagnostics(hostOptions.DiagnosticsCapacity);

            _builder.Services.AddSingleton<IApplicationContext>(context);
            _builder.Services.AddSingleton(Options.Create(hostOptions));
            _builder.Services.Configure<HostOptions>(options =>
                options.ShutdownTimeout = hostOptions.ShutdownTimeout);
            _builder.Services.TryAddSingleton<IHostDiagnostics>(runtimeDiagnostics);
            _builder.Services.TryAddSingleton<IUiDispatcher, UnavailableUiDispatcher>();
            _builder.Services.TryAddSingleton<IModuleRegistry>(moduleRegistry);
            _builder.Services.TryAddSingleton(lifecyclePipeline);

            buildStage = "ModuleServices";
            moduleRegistry.ConfigureServicesAsync(context, _builder.Services, buildDiagnostics)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            buildStage = "UserServices";
            ApplyUserServiceConfigurations();

            buildStage = "GenericHost";
            genericHost = _builder.Build();

            var diagnostics = genericHost.Services.GetRequiredService<IHostDiagnostics>();

            WriteDiagnostic(diagnostics, new HostDiagnosticRecord(
                HostDiagnosticIds.HostBuilt,
                "Application host has been built.",
                HostDiagnosticSeverity.Info));

            if (!ReferenceEquals(buildDiagnostics, diagnostics))
            {
                WriteDiagnostic(buildDiagnostics, new HostDiagnosticRecord(
                    HostDiagnosticIds.HostBuilt,
                    "Application host has been built.",
                    HostDiagnosticSeverity.Info));
            }

            return new DefaultApplicationHost(
                genericHost,
                context,
                diagnostics,
                LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host", diagnostics),
                moduleRegistry,
                lifecyclePipeline,
                hostOptions);
        }
        catch (Exception exception)
        {
            var failure = new HostDiagnosticRecord(
                HostDiagnosticIds.HostBuildFailed,
                "Application host failed to build.",
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>
                {
                    ["stage"] = buildStage,
                    ["exceptionType"] = exception.GetType().FullName,
                    ["details"] = exception.Message,
                },
            };

            WriteDiagnostic(buildDiagnostics, failure);

            if (runtimeDiagnostics is not null && !ReferenceEquals(runtimeDiagnostics, buildDiagnostics))
            {
                WriteDiagnostic(runtimeDiagnostics, failure);
            }

            if (buildStage == "ModuleGraph")
            {
                WriteDiagnostic(buildDiagnostics, new HostDiagnosticRecord(
                    HostDiagnosticIds.ModuleGraphFailed,
                    "Module dependency graph failed to build.",
                    HostDiagnosticSeverity.Error)
                {
                    Context = failure.Context,
                });
            }

            try
            {
                genericHost?.Dispose();
            }
            catch
            {
                // Preserve the original build failure.
            }

            if (moduleRegistry is not null)
            {
                try
                {
                    moduleRegistry.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                catch
                {
                    // Preserve the original build failure.
                }
            }

            throw;
        }
    }

    private ApplicationHostOptions CreateHostOptions()
    {
        var options = new ApplicationHostOptions();

        foreach (var configure in _configureHostActions)
        {
            configure(options);
        }

        return options;
    }

    private void ApplyUserServiceConfigurations()
    {
        _applyingUserServices = true;

        try
        {
            foreach (var configureServices in _configureServicesActions)
            {
                configureServices(_services);
            }
        }
        finally
        {
            _applyingUserServices = false;
        }
    }

    private ApplicationContext CreateApplicationContext(ApplicationHostOptions hostOptions)
    {
        var applicationId = GetRequiredExplicitValue(
            hostOptions.ApplicationId,
            nameof(hostOptions.ApplicationId));
        var applicationName = hostOptions.ApplicationName is not null
            ? GetRequiredExplicitValue(
                hostOptions.ApplicationName,
                nameof(hostOptions.ApplicationName))
            : string.IsNullOrWhiteSpace(_builder.Environment.ApplicationName)
            ? applicationId
            : _builder.Environment.ApplicationName;
        ValidateApplicationName(applicationName);

        var applicationVersion = ResolveApplicationVersion(hostOptions.ApplicationVersion);
        var environmentName = GetRequiredValue(
            _builder.Environment.EnvironmentName,
            nameof(IApplicationContext.EnvironmentName));
        var contentRootPath = NormalizeAbsolutePath(
            _builder.Environment.ContentRootPath,
            nameof(IApplicationContext.ContentRootPath));
        var localApplicationData = NormalizeAbsolutePath(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            nameof(Environment.SpecialFolder.LocalApplicationData));
        var appDataPath = NormalizeAbsolutePath(
            Path.Combine(localApplicationData, applicationName),
            nameof(IApplicationContext.AppDataPath));

        return new ApplicationContext(
            applicationId,
            Guid.NewGuid(),
            applicationName,
            applicationVersion,
            environmentName,
            contentRootPath,
            appDataPath,
            _args);
    }

    private static string ResolveApplicationVersion(string? configuredVersion)
    {
        if (configuredVersion is not null)
        {
            return GetRequiredExplicitValue(
                configuredVersion,
                nameof(ApplicationHostOptions.ApplicationVersion));
        }

        var entryAssembly = Assembly.GetEntryAssembly();
        var informationalVersion = entryAssembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        var assemblyVersion = entryAssembly?.GetName().Version?.ToString();

        return GetRequiredValue(
            assemblyVersion,
            nameof(IApplicationContext.ApplicationVersion));
    }

    private static string GetRequiredExplicitValue(string? value, string fieldName)
    {
        var requiredValue = GetRequiredValue(value, fieldName);

        if (!string.Equals(requiredValue, requiredValue.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{fieldName} cannot contain leading or trailing whitespace.",
                fieldName);
        }

        return requiredValue;
    }

    private static string GetRequiredValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value;
    }

    private static void ValidateApplicationName(string applicationName)
    {
        if (!string.Equals(applicationName, applicationName.Trim(), StringComparison.Ordinal) ||
            applicationName is "." or ".." ||
            applicationName.Contains('/') ||
            applicationName.Contains('\\') ||
            applicationName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            OperatingSystem.IsWindows() &&
            (applicationName.EndsWith('.') ||
             IsWindowsReservedDeviceName(applicationName)))
        {
            throw new ArgumentException(
                "ApplicationName must be a single valid directory name.",
                nameof(ApplicationHostOptions.ApplicationName));
        }
    }

    private static bool IsWindowsReservedDeviceName(string applicationName)
    {
        var stem = applicationName.Split('.', 2)[0];

        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stem.Length == 4 &&
               stem[3] is >= '1' and <= '9' &&
               (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeAbsolutePath(string? path, string fieldName)
    {
        var requiredPath = GetRequiredValue(path, fieldName);
        var normalizedPath = Path.GetFullPath(requiredPath);

        if (!Path.IsPathFullyQualified(normalizedPath))
        {
            throw new InvalidOperationException($"{fieldName} must resolve to an absolute path.");
        }

        return Path.TrimEndingDirectorySeparator(normalizedPath);
    }

    private IHostDiagnostics? GetRegisteredDiagnosticsInstance()
    {
        return _builder.Services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(IHostDiagnostics))
            ?.ImplementationInstance as IHostDiagnostics;
    }

    private static void ValidateHostOptions(ApplicationHostOptions options)
    {
        if (options.ShutdownTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.ShutdownTimeout),
                options.ShutdownTimeout,
                "Shutdown timeout must be greater than zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.DiagnosticsCapacity);
    }

    private static void WriteDiagnostic(IHostDiagnostics diagnostics, HostDiagnosticRecord record)
    {
        try
        {
            diagnostics.Write(record);
        }
        catch
        {
            // Diagnostics must not change the build result.
        }
    }

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("Application host builder is frozen after Build.");
        }
    }

    private sealed class GuardedServiceCollection(
        IServiceCollection inner,
        Func<bool> canMutate) : IServiceCollection
    {
        public ServiceDescriptor this[int index]
        {
            get => inner[index];
            set
            {
                ThrowIfFrozen();
                inner[index] = value;
            }
        }

        public int Count => inner.Count;

        public bool IsReadOnly => !canMutate() || inner.IsReadOnly;

        public void Add(ServiceDescriptor item)
        {
            ThrowIfFrozen();
            inner.Add(item);
        }

        public void Clear()
        {
            ThrowIfFrozen();
            inner.Clear();
        }

        public bool Contains(ServiceDescriptor item)
        {
            return inner.Contains(item);
        }

        public void CopyTo(ServiceDescriptor[] array, int arrayIndex)
        {
            inner.CopyTo(array, arrayIndex);
        }

        public IEnumerator<ServiceDescriptor> GetEnumerator()
        {
            return inner.GetEnumerator();
        }

        public int IndexOf(ServiceDescriptor item)
        {
            return inner.IndexOf(item);
        }

        public void Insert(int index, ServiceDescriptor item)
        {
            ThrowIfFrozen();
            inner.Insert(index, item);
        }

        public bool Remove(ServiceDescriptor item)
        {
            ThrowIfFrozen();

            return inner.Remove(item);
        }

        public void RemoveAt(int index)
        {
            ThrowIfFrozen();
            inner.RemoveAt(index);
        }

        private void ThrowIfFrozen()
        {
            if (!canMutate())
            {
                throw new InvalidOperationException(
                    "Application services can only be modified while ConfigureServices callbacks are applied during Build.");
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class GuardedConfigurationManager : IConfigurationManager
    {
        private readonly IConfigurationManager _inner;
        private readonly GuardedDictionary<string, object> _properties;
        private readonly GuardedList<IConfigurationSource> _sources;
        private readonly Action _throwIfFrozen;

        public GuardedConfigurationManager(
            IConfigurationManager inner,
            Action throwIfFrozen)
        {
            _inner = inner;
            _throwIfFrozen = throwIfFrozen;
            _properties = new GuardedDictionary<string, object>(inner.Properties, throwIfFrozen);
            _sources = new GuardedList<IConfigurationSource>(inner.Sources, throwIfFrozen);
        }

        public string? this[string key]
        {
            get => _inner[key];
            set
            {
                _throwIfFrozen();
                _inner[key] = value;
            }
        }

        public IDictionary<string, object> Properties => _properties;

        public IList<IConfigurationSource> Sources => _sources;

        public IConfigurationRoot Build()
        {
            return _inner.Build();
        }

        public IConfigurationBuilder Add(IConfigurationSource source)
        {
            _throwIfFrozen();
            _inner.Add(source);

            return this;
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return _inner.GetChildren();
        }

        public IChangeToken GetReloadToken()
        {
            return _inner.GetReloadToken();
        }

        public IConfigurationSection GetSection(string key)
        {
            return _inner.GetSection(key);
        }
    }

    private sealed class GuardedList<T>(
        IList<T> inner,
        Action throwIfFrozen) : IList<T>
    {
        public T this[int index]
        {
            get => inner[index];
            set
            {
                throwIfFrozen();
                inner[index] = value;
            }
        }

        public int Count => inner.Count;

        public bool IsReadOnly => inner.IsReadOnly;

        public void Add(T item)
        {
            throwIfFrozen();
            inner.Add(item);
        }

        public void Clear()
        {
            throwIfFrozen();
            inner.Clear();
        }

        public bool Contains(T item)
        {
            return inner.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            inner.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return inner.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return inner.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            throwIfFrozen();
            inner.Insert(index, item);
        }

        public bool Remove(T item)
        {
            throwIfFrozen();

            return inner.Remove(item);
        }

        public void RemoveAt(int index)
        {
            throwIfFrozen();
            inner.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class GuardedDictionary<TKey, TValue>(
        IDictionary<TKey, TValue> inner,
        Action throwIfFrozen) : IDictionary<TKey, TValue>
        where TKey : notnull
    {
        public TValue this[TKey key]
        {
            get => inner[key];
            set
            {
                throwIfFrozen();
                inner[key] = value;
            }
        }

        public ICollection<TKey> Keys => inner.Keys;

        public ICollection<TValue> Values => inner.Values;

        public int Count => inner.Count;

        public bool IsReadOnly => inner.IsReadOnly;

        public void Add(TKey key, TValue value)
        {
            throwIfFrozen();
            inner.Add(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            throwIfFrozen();
            inner.Add(item);
        }

        public void Clear()
        {
            throwIfFrozen();
            inner.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return inner.Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return inner.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            inner.CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return inner.GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            throwIfFrozen();

            return inner.Remove(key);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throwIfFrozen();

            return inner.Remove(item);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return inner.TryGetValue(key, out value!);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
