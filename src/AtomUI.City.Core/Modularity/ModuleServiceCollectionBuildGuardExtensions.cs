using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents module service collection build guard extensions.
/// </summary>
public static class ModuleServiceCollectionBuildGuardExtensions
{
    /// <summary>
    /// Executes the build service provider operation.
    /// </summary>
    public static ServiceProvider BuildServiceProvider(this ModuleServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return ThrowTemporaryProviderNotAllowed();
    }

    /// <summary>
    /// Executes the build service provider operation.
    /// </summary>
    public static ServiceProvider BuildServiceProvider(
        this ModuleServiceCollection services,
        bool validateScopes)
    {
        ArgumentNullException.ThrowIfNull(services);

        return ThrowTemporaryProviderNotAllowed();
    }

    /// <summary>
    /// Executes the build service provider operation.
    /// </summary>
    public static ServiceProvider BuildServiceProvider(
        this ModuleServiceCollection services,
        ServiceProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        return ThrowTemporaryProviderNotAllowed();
    }

    private static ServiceProvider ThrowTemporaryProviderNotAllowed()
    {
        throw new InvalidOperationException(
            "Modules must not build a temporary service provider during service configuration.");
    }
}
