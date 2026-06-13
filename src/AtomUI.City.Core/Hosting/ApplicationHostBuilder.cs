using System.Collections;
using AtomUI.City.Diagnostics;
using AtomUI.City.Lifecycle;
using AtomUI.City.Modularity;
using AtomUI.City.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace AtomUI.City.Hosting;

public sealed class ApplicationHostBuilder : IApplicationHostBuilder
{
    private readonly string[] _args;
    private readonly HostApplicationBuilder _builder;
    private readonly List<Action<ApplicationHostOptions>> _configureHostActions = [];
    private readonly GuardedConfigurationManager _configuration;
    private readonly GuardedDictionary<string, object?> _properties;
    private readonly GuardedServiceCollection _services;
    private readonly Dictionary<string, object?> _propertiesStore = new(StringComparer.Ordinal);
    private bool _built;

    internal ApplicationHostBuilder(string[] args)
    {
        _args = args.ToArray();
        _builder = Host.CreateApplicationBuilder(_args);
        _configuration = new GuardedConfigurationManager(_builder.Configuration, ThrowIfBuilt);
        _properties = new GuardedDictionary<string, object?>(_propertiesStore, ThrowIfBuilt);
        _services = new GuardedServiceCollection(_builder.Services, ThrowIfBuilt);
    }

    public IServiceCollection Services => _services;

    public IConfigurationManager Configuration => _configuration;

    public IDictionary<string, object?> Properties => _properties;

    public IApplicationHostBuilder ConfigureServices(Action<IServiceCollection> configureServices)
    {
        ArgumentNullException.ThrowIfNull(configureServices);
        ThrowIfBuilt();

        configureServices(Services);

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

        var hostOptions = CreateHostOptions();
        var context = CreateApplicationContext(hostOptions);
        var moduleRegistrations = ModuleRegistrationStore.GetRegistrations(this);

        _built = true;

        var moduleRegistry = ModuleRegistry.Create(moduleRegistrations);
        var buildDiagnostics = GetOrCreateBuildDiagnostics();

        _builder.Services.AddSingleton(context);
        _builder.Services.AddSingleton<IApplicationContext>(context);
        _builder.Services.AddSingleton(Options.Create(hostOptions));
        _builder.Services.TryAddSingleton<IHostDiagnostics>(buildDiagnostics);
        _builder.Services.TryAddSingleton<IUiDispatcher, UnavailableUiDispatcher>();
        _builder.Services.TryAddSingleton<IModuleRegistry>(moduleRegistry);

        moduleRegistry.ConfigureServicesAsync(context, _builder.Services).AsTask().GetAwaiter().GetResult();

        var genericHost = _builder.Build();

        context.Services = genericHost.Services;
        var diagnostics = genericHost.Services.GetRequiredService<IHostDiagnostics>();

        diagnostics.Write(new HostDiagnosticRecord(
            HostDiagnosticIds.HostBuilt,
            "Application host has been built.",
            HostDiagnosticSeverity.Info));

        return new DefaultApplicationHost(
            genericHost,
            context,
            diagnostics,
            LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host"),
            moduleRegistry);
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

    private ApplicationContext CreateApplicationContext(ApplicationHostOptions hostOptions)
    {
        var applicationName = !string.IsNullOrWhiteSpace(hostOptions.ApplicationName)
            ? hostOptions.ApplicationName
            : string.IsNullOrWhiteSpace(_builder.Environment.ApplicationName)
            ? "AtomUI.City.Application"
            : _builder.Environment.ApplicationName;
        var context = new ApplicationContext
        {
            ApplicationName = applicationName,
            EnvironmentName = _builder.Environment.EnvironmentName,
            ContentRootPath = _builder.Environment.ContentRootPath,
            AppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                applicationName),
            StartupArguments = _args,
            Configuration = Configuration,
        };

        foreach (var property in _propertiesStore)
        {
            context.Properties[property.Key] = property.Value;
        }

        return context;
    }

    private IHostDiagnostics GetOrCreateBuildDiagnostics()
    {
        var existing = _builder.Services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(IHostDiagnostics))
            ?.ImplementationInstance as IHostDiagnostics;

        return existing ?? new InMemoryHostDiagnostics();
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
        Action throwIfFrozen) : IServiceCollection
    {
        public ServiceDescriptor this[int index]
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

        public void Add(ServiceDescriptor item)
        {
            throwIfFrozen();
            inner.Add(item);
        }

        public void Clear()
        {
            throwIfFrozen();
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
            throwIfFrozen();
            inner.Insert(index, item);
        }

        public bool Remove(ServiceDescriptor item)
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
