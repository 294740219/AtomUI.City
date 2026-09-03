using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Mvp.Foundation;

[ServiceRegistrationOwner]
public sealed class FoundationModule : ModuleBase;

public interface IMvpClock
{
    long GetTimestamp();
}

[Service(ServiceLifetime.Singleton)]
[ExposeServices(typeof(IMvpClock))]
public sealed class MvpClock : IMvpClock
{
    public long GetTimestamp() => Environment.TickCount64;
}

public sealed class FoundationScope : IScopedDependency
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class FoundationNonce : ITransientDependency
{
    public Guid Id { get; } = Guid.NewGuid();
}

public interface IFoundationStrategy
{
    string Name { get; }
}

[Service(ServiceLifetime.Singleton, Key = "primary")]
[ExposeServices(typeof(IFoundationStrategy))]
public sealed class PrimaryFoundationStrategy : IFoundationStrategy
{
    public string Name => "primary";
}

[Service(ServiceLifetime.Singleton, Key = "secondary")]
[ExposeServices(typeof(IFoundationStrategy))]
public sealed class SecondaryFoundationStrategy : IFoundationStrategy
{
    public string Name => "secondary";
}
