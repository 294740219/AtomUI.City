using AtomUI.City.Hosting;

namespace AtomUI.City.Testing;

public sealed class TestHostBuilder
{
    private readonly Dictionary<string, object?> _properties = new(StringComparer.Ordinal);
    private string? _directoryName;
    private bool _keepDirectoryOnDispose;
    private bool _built;

    public TestHostBuilder UseProperty(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ThrowIfBuilt();

        _properties[key] = value;

        return this;
    }

    public TestHostBuilder UseDirectoryName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ThrowIfBuilt();

        _directoryName = name;

        return this;
    }

    public TestHostBuilder KeepDirectoryOnDispose()
    {
        ThrowIfBuilt();

        _keepDirectoryOnDispose = true;

        return this;
    }

    public TestHost Build()
    {
        ThrowIfBuilt();

        var applicationContext = new ApplicationContext();

        foreach (var property in _properties)
        {
            applicationContext.Properties[property.Key] = property.Value;
        }

        var diagnostics = new TestDiagnostics();
        var directory = TestDirectory.Create(_directoryName ?? "host", _keepDirectoryOnDispose);

        _built = true;

        return new TestHost(
            applicationContext,
            directory,
            new FakeUiDispatcher(diagnostics),
            new DeterministicScheduler(diagnostics),
            diagnostics);
    }

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("The test host builder has already built a host and is frozen.");
        }
    }
}
