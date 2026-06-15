using AtomUI.City.Security;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Data.Tests;

public sealed class DataRegistrationTests
{
    [Fact]
    public void AddDataRegistersCoreServices()
    {
        var services = new ServiceCollection();

        services.AddSecurity();
        services.AddData();

        using var serviceProvider = services.BuildServiceProvider();
        var pipeline = serviceProvider.GetRequiredService<IDataRequestPipeline>();
        var credentialProvider = serviceProvider.GetRequiredService<IDataCredentialProvider>();
        var cache = serviceProvider.GetRequiredService<IDataRequestCache>();
        var connectionManager = serviceProvider.GetRequiredService<DataConnectionManager>();

        Assert.IsType<DataRequestPipeline>(pipeline);
        Assert.IsType<AccessTokenCredentialProvider>(credentialProvider);
        Assert.IsType<InMemoryDataRequestCache>(cache);
        Assert.NotNull(connectionManager);
    }

    [Fact]
    public void AddDataRegistersDefaultDiagnosticsAndTransports()
    {
        var services = new ServiceCollection();

        services.AddData();

        using var serviceProvider = services.BuildServiceProvider();
        var diagnostics = serviceProvider.GetRequiredService<IDataDiagnostics>();
        var transports = serviceProvider.GetServices<IRequestResponseTransport>().ToArray();
        var clientRegistry = serviceProvider.GetRequiredService<DataClientRegistry>();
        var clientFactory = serviceProvider.GetRequiredService<IDataClientFactory>();

        Assert.IsType<InMemoryDataDiagnostics>(diagnostics);
        Assert.Contains(transports, transport => transport is HttpDataTransport);
        Assert.Contains(transports, transport => transport is GrpcDataTransport);
        Assert.Contains(transports, transport => transport is SignalRDataTransport);
        Assert.Equal(
            [DataTransportKind.Http, DataTransportKind.Grpc, DataTransportKind.SignalR],
            transports.Select(transport => transport.Kind).OrderBy(kind => kind).ToArray());
        Assert.Same(clientRegistry, clientFactory);
    }

    [Fact]
    public void AddDataCanBeCalledRepeatedlyWithoutDuplicatingDefaultTransports()
    {
        var services = new ServiceCollection();

        services.AddData();
        services.AddData();

        using var serviceProvider = services.BuildServiceProvider();
        var transportTypes = serviceProvider
            .GetServices<IRequestResponseTransport>()
            .Select(transport => transport.GetType())
            .ToArray();

        Assert.Equal(1, transportTypes.Count(type => type == typeof(HttpDataTransport)));
        Assert.Equal(1, transportTypes.Count(type => type == typeof(GrpcDataTransport)));
        Assert.Equal(1, transportTypes.Count(type => type == typeof(SignalRDataTransport)));
    }

    [Fact]
    public async Task DataRequestPipelineUsesFirstTransportForDuplicateKinds()
    {
        var primary = new TestTransport(DataTransportKind.Http, "primary");
        var replacement = new TestTransport(DataTransportKind.Http, "replacement");
        var pipeline = new DataRequestPipeline([primary, replacement]);

        var result = await pipeline.SendAsync(new DataRequest<string>(
            "registration",
            "duplicate-transport",
            DataTransportKind.Http));

        Assert.True(result.Succeeded);
        Assert.Equal("primary", result.Value);
    }

    [Fact]
    public async Task AddDataKeepsTransportRegisteredBeforeDefaultsForSameKind()
    {
        var services = new ServiceCollection();
        var customTransport = new TestTransport(DataTransportKind.Http, "custom");

        services.AddSingleton<IRequestResponseTransport>(customTransport);
        services.AddData();

        using var serviceProvider = services.BuildServiceProvider();
        var pipeline = serviceProvider.GetRequiredService<IDataRequestPipeline>();
        var transports = serviceProvider.GetServices<IRequestResponseTransport>().ToArray();

        var result = await pipeline.SendAsync(new DataRequest<string>(
            "registration",
            "transport-override",
            DataTransportKind.Http));

        Assert.True(result.Succeeded);
        Assert.Equal("custom", result.Value);
        Assert.Same(customTransport, transports.First(transport => transport.Kind == DataTransportKind.Http));
        Assert.Contains(transports, transport => transport is GrpcDataTransport);
        Assert.Contains(transports, transport => transport is SignalRDataTransport);
    }

    [Fact]
    public async Task AddDataSupportsBearerRequestWithoutFullSecurityRegistration()
    {
        var services = new ServiceCollection();

        services.AddData();

        using var serviceProvider = services.BuildServiceProvider();
        var pipeline = serviceProvider.GetRequiredService<IDataRequestPipeline>();

        var result = await pipeline.SendAsync(
            new DataRequest<string>("catalog", "secure-items", DataTransportKind.Http)
            {
                Authentication = DataAuthenticationOptions.Bearer(),
            });

        Assert.False(result.Succeeded);
        Assert.Equal(DataErrorKind.CredentialUnavailable, result.Error?.Kind);
    }

    private sealed class TestTransport : IRequestResponseTransport
    {
        private readonly string _response;

        public TestTransport(DataTransportKind kind, string response)
        {
            Kind = kind;
            _response = response;
        }

        public DataTransportKind Kind { get; }

        public ValueTask<DataResult<TResponse>> SendAsync<TResponse>(
            DataRequest<TResponse> request,
            DataRequestContext context,
            CancellationToken cancellationToken = default)
        {
            return _response is TResponse response
                ? ValueTask.FromResult(DataResult<TResponse>.Success(response))
                : ValueTask.FromResult(DataResult<TResponse>.Failed(
                    new DataError(
                        DataErrorKind.SerializationError,
                        $"Test response cannot be cast to '{typeof(TResponse).FullName}'.")));
        }
    }
}
