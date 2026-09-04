using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;

namespace TodoCli;

[ApplicationModule]
[ServiceRegistrationOwner]
public sealed class CliModule : ModuleBase;
