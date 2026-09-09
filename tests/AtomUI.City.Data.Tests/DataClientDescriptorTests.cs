namespace AtomUI.City.Data.Tests;

public sealed class DataClientDescriptorTests
{
    [Fact]
    public void CatalogRegistersGeneratedDescriptorAndLeaseRevokesIt()
    {
        var catalog = new DataClientDescriptorCatalog();
        catalog.RegisterGenerated<TestRegistrar>();
        Assert.True(catalog.TryGet("generated-client", out var descriptor));
        Assert.Equal(DataTransportKind.Http, descriptor!.TransportKind);
        Assert.Equal("query", Assert.Single(descriptor.Operations).OperationName);

        var lease = catalog.Register(new DataClientDescriptor(
            "leased-client",
            typeof(TestClient),
            DataTransportKind.Grpc,
            "1",
            []));
        Assert.True(catalog.TryGet("leased-client", out _));
        lease.Dispose();
        Assert.False(catalog.TryGet("leased-client", out _));
    }

    [Fact]
    public void DescriptorRejectsDuplicateOperationNames()
    {
        var operation = new DataOperationDescriptor(
            "duplicate",
            typeof(object),
            typeof(string),
            DataAccessMode.Query);

        Assert.Throws<ArgumentException>(() => new DataClientDescriptor(
            "invalid",
            typeof(TestClient),
            DataTransportKind.Http,
            "1",
            [operation, operation]));
    }

    [Fact]
    public void GeneratedRegistrationRollsBackAllDescriptorsWhenRegistrarFails()
    {
        var catalog = new DataClientDescriptorCatalog();
        using var existing = catalog.Register(CreateDescriptor("existing"));

        Assert.Throws<InvalidOperationException>(() => catalog.RegisterGenerated<FailingRegistrar>());

        Assert.True(catalog.TryGet("existing", out _));
        Assert.False(catalog.TryGet("partial", out _));
        Assert.Single(catalog.Snapshot);
    }

    [Fact]
    public void GrpcOptionsRejectBinaryMetadataFromStringContract()
    {
        Assert.Throws<ArgumentException>(() => new GrpcCallOptions
        {
            Metadata = new Dictionary<string, string> { ["trace-bin"] = "not-binary" },
        });
    }

    public sealed class TestRegistrar : IDataClientDescriptorRegistrar
    {
        public void Register(DataClientDescriptorCatalog catalog)
        {
            catalog.Register(new DataClientDescriptor(
                "generated-client",
                typeof(TestClient),
                DataTransportKind.Http,
                "1",
                [new DataOperationDescriptor("query", typeof(object), typeof(string), DataAccessMode.Query)]));
        }
    }

    public sealed class FailingRegistrar : IDataClientDescriptorRegistrar
    {
        public void Register(DataClientDescriptorCatalog catalog)
        {
            catalog.Register(CreateDescriptor("partial"));
            catalog.Register(CreateDescriptor("existing"));
        }
    }

    private static DataClientDescriptor CreateDescriptor(string clientId) => new(
        clientId,
        typeof(TestClient),
        DataTransportKind.Http,
        "1",
        []);

    private sealed class TestClient : IDataClient
    {
        public string ClientId => "test-client";
    }
}
