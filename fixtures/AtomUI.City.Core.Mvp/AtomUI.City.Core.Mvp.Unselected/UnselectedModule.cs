using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AtomUI.City.Core.Mvp.Unselected;

[ServiceRegistrationOwner]
public sealed class UnselectedModule : ModuleBase;

[Service(ServiceLifetime.Singleton)]
[ExposeServices(typeof(IHostedService))]
public sealed class UnselectedHostedService : IHostedService
{
    private static int _startCount;
    public static int StartCount => Volatile.Read(ref _startCount);
    public static void Reset() => Interlocked.Exchange(ref _startCount, 0);
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _startCount);
        throw new InvalidOperationException("Unselected hosted service was activated.");
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

[Service(ServiceLifetime.Singleton)]
public sealed class UnselectedSingleton
{
    private static int _createdCount;
    public static int CreatedCount => Volatile.Read(ref _createdCount);
    public static void Reset() => Interlocked.Exchange(ref _createdCount, 0);
    public UnselectedSingleton() => Interlocked.Increment(ref _createdCount);
}

public sealed class UnselectedScope : IScopedDependency;

public sealed class UnselectedTransient : ITransientDependency;
