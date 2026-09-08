using AtomUI.City.Core.Modularity;

namespace AtomUI.City.Routing;

public sealed class RoutingModule : ModuleBase
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddRouting();
    }
}
