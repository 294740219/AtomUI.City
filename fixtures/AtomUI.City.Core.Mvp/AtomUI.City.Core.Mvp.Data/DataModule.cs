using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Core.Mvp.Foundation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Mvp.Data;

[ServiceRegistrationOwner]
[DependsOn(typeof(FoundationModule))]
public sealed class DataModule : ModuleBase
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IDataPolicy, PreconfiguredDataPolicy>();
    }
}

public interface IRecordReader
{
    string Read();
}

public interface IRecordWriter
{
    void Write(string value);
}

[ScopedService(typeof(IRecordReader), typeof(IRecordWriter))]
public sealed class RecordStore : IRecordReader, IRecordWriter
{
    private string _value = "record";
    public string Read() => _value;
    public void Write(string value) => _value = value;
}

[Service(ServiceLifetime.Singleton)]
public sealed class DataCache
{
    public Guid Id { get; } = Guid.NewGuid();
}

[ScopedService]
public sealed class DataUnitOfWork : IDisposable
{
    private static int _disposeCount;
    public static int DisposeCount => Volatile.Read(ref _disposeCount);
    public static void Reset() => Interlocked.Exchange(ref _disposeCount, 0);
    public void Dispose() => Interlocked.Increment(ref _disposeCount);
}

[Service(ServiceLifetime.Transient)]
public sealed class DataSerializer
{
    public string Serialize(string value) => value;
}

public interface IDataPolicy
{
    string Name { get; }
}

[Service(ServiceLifetime.Singleton, TryAdd = true)]
[ExposeServices(typeof(IDataPolicy))]
public sealed class DefaultDataPolicy : IDataPolicy
{
    public string Name => "generated";
}

public sealed class PreconfiguredDataPolicy : IDataPolicy
{
    public string Name => "preconfigured";
}
