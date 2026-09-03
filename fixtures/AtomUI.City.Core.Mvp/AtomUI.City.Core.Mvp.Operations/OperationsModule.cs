using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Core.Mvp.Data;
using AtomUI.City.Core.Mvp.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.Core.Mvp.Operations;

[ServiceRegistrationOwner]
[DependsOn(typeof(FoundationModule))]
[DependsOn(typeof(DataModule))]
public sealed class OperationsModule : ModuleBase;

[ScopedService]
public sealed class OrderProcessor(IRecordReader records, IMvpClock clock)
{
    public string Execute() => $"{records.Read()}:{clock.GetTimestamp()}";
}

[Service(ServiceLifetime.Transient)]
public sealed class CommandDispatcher
{
    public Guid Id { get; } = Guid.NewGuid();
}

[Service(ServiceLifetime.Singleton)]
public sealed class JobCoordinator
{
    public Guid Id { get; } = Guid.NewGuid();
}

[Service(ServiceLifetime.Singleton)]
[ExposeServices(typeof(IHostedService), typeof(SelectedHostedService))]
public sealed class SelectedHostedService : IHostedService
{
    private static int _startCount;
    private static int _stopCount;
    public static int StartCount => Volatile.Read(ref _startCount);
    public static int StopCount => Volatile.Read(ref _stopCount);
    public static void Reset()
    {
        Interlocked.Exchange(ref _startCount, 0);
        Interlocked.Exchange(ref _stopCount, 0);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _startCount);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _stopCount);
        return Task.CompletedTask;
    }
}

public sealed class OperationProbe : ITransientDependency
{
    public Guid Id { get; } = Guid.NewGuid();
}
