using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Core.Mvp.Foundation;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Mvp.Conflict;

[ServiceRegistrationOwner]
[DependsOn(typeof(FoundationModule))]
public sealed class ConflictModule : ModuleBase;

[Service(ServiceLifetime.Singleton)]
[ExposeServices(typeof(IMvpClock))]
public sealed class ConflictingClock : IMvpClock
{
    public long GetTimestamp() => -1;
}
