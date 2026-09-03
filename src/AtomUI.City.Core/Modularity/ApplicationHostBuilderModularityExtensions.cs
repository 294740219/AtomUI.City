using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents application host builder modularity extensions.
/// </summary>
public static class ApplicationHostBuilderModularityExtensions
{
    /// <summary>
    /// Executes the use module operation.
    /// </summary>
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
