using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.Modularity;

public static class ApplicationHostBuilderModularityExtensions
{
    public static IApplicationHostBuilder UseModule<TModule>(this IApplicationHostBuilder builder)
        where TModule : IModule, new()
    {
        ArgumentNullException.ThrowIfNull(builder);

        ModuleRegistrationStore.Add(
            builder,
            new ModuleRegistration(typeof(TModule), static () => new TModule()));

        return builder;
    }
}
