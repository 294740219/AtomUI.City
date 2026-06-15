using AtomUI.City.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddData(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient();
        services.TryAddSingleton<IAccessTokenProvider, UnavailableAccessTokenProvider>();
        services.TryAddSingleton<IDataDiagnostics, InMemoryDataDiagnostics>();
        services.TryAddSingleton<IDataCredentialProvider, AccessTokenCredentialProvider>();
        services.TryAddSingleton<IDataRequestCache, InMemoryDataRequestCache>();
        services.TryAddSingleton<DataConnectionManager>();
        services.TryAddSingleton<DataClientRegistry>();
        services.TryAddSingleton<IDataClientFactory>(
            serviceProvider => serviceProvider.GetRequiredService<DataClientRegistry>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestResponseTransport, HttpDataTransport>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestResponseTransport, GrpcDataTransport>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRequestResponseTransport, SignalRDataTransport>());
        services.TryAddSingleton<IDataRequestPipeline>(serviceProvider => new DataRequestPipeline(
            serviceProvider.GetServices<IRequestResponseTransport>(),
            serviceProvider.GetService<IDataCredentialProvider>(),
            serviceProvider.GetService<IDataDiagnostics>(),
            serviceProvider.GetService<IDataRequestCache>()));

        return services;
    }
}
