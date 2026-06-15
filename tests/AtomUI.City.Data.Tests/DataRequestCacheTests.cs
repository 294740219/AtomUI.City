namespace AtomUI.City.Data.Tests;

public sealed class DataRequestCacheTests
{
    [Fact]
    public async Task InMemoryCacheStoresAndReturnsValuesByCacheKey()
    {
        var cache = new InMemoryDataRequestCache();
        var key = CreateKey("items:v1");

        var miss = await cache.TryGetAsync<string>(key);
        await cache.SetAsync(key, "cached");
        var hit = await cache.TryGetAsync<string>(key);

        Assert.False(miss.IsHit);
        Assert.True(hit.IsHit);
        Assert.Equal("cached", hit.Value);
    }

    [Fact]
    public async Task InMemoryCacheStoresNullReferenceValuesByCacheKey()
    {
        var cache = new InMemoryDataRequestCache();
        var key = CreateKey("nullable:v1");

        await cache.SetAsync<string?>(key, null);
        var lookup = await cache.TryGetAsync<string?>(key);

        Assert.True(lookup.IsHit);
        Assert.Null(lookup.Value);
    }

    [Fact]
    public async Task InMemoryCacheInvalidatesEntryByCacheKey()
    {
        var cache = new InMemoryDataRequestCache();
        var key = CreateKey("items:v1");

        await cache.SetAsync(key, "cached");
        await cache.InvalidateAsync(key);
        var lookup = await cache.TryGetAsync<string>(key);

        Assert.False(lookup.IsHit);
    }

    [Fact]
    public async Task InMemoryCacheIsolatesEntriesByPrincipalRevision()
    {
        var cache = new InMemoryDataRequestCache();
        var userAKey = CreateKey("items:v1") with { PrincipalRevision = "user-a" };
        var userBKey = CreateKey("items:v1") with { PrincipalRevision = "user-b" };

        await cache.SetAsync(userAKey, "user-a-cache");
        var userBLookup = await cache.TryGetAsync<string>(userBKey);

        Assert.False(userBLookup.IsHit);
    }

    [Theory]
    [InlineData("clientId")]
    [InlineData("operationName")]
    [InlineData("requestFingerprint")]
    [InlineData("authenticationScheme")]
    [InlineData("principalRevision")]
    [InlineData("permissionRevision")]
    [InlineData("clientVersion")]
    [InlineData("policyVersion")]
    public void DataCacheKeyRejectsMissingRequiredParts(string missingPart)
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateKeyWithMissingPart(missingPart));

        Assert.Equal(ToConstructorParameterName(missingPart), exception.ParamName);
    }

    [Fact]
    public async Task InMemoryCacheWritesInvalidationDiagnostic()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        var cache = new InMemoryDataRequestCache(diagnostics);
        var key = CreateKey("items:v1");

        await cache.InvalidateAsync(key);

        var record = Assert.Single(
            diagnostics.Records,
            record => record.Code == DataDiagnosticIds.CacheInvalidated);
        Assert.Equal("catalog", record.ClientId);
        Assert.Equal("get-items", record.OperationName);
        Assert.Equal(DataTransportKind.Http, record.TransportKind);
    }

    private static DataCacheKey CreateKey(string requestFingerprint)
    {
        return new DataCacheKey(
            "catalog",
            "get-items",
            DataTransportKind.Http,
            DataAccessMode.Query,
            requestFingerprint,
            "Anonymous",
            "anonymous",
            "default",
            PluginContributionId: null,
            "default",
            "default");
    }

    private static DataCacheKey CreateKeyWithMissingPart(string missingPart)
    {
        var clientId = "catalog";
        var operationName = "get-items";
        var requestFingerprint = "items:v1";
        var authenticationScheme = "Anonymous";
        var principalRevision = "anonymous";
        var permissionRevision = "default";
        var clientVersion = "default";
        var policyVersion = "default";

        switch (missingPart)
        {
            case "clientId":
                clientId = " ";
                break;
            case "operationName":
                operationName = " ";
                break;
            case "requestFingerprint":
                requestFingerprint = " ";
                break;
            case "authenticationScheme":
                authenticationScheme = " ";
                break;
            case "principalRevision":
                principalRevision = " ";
                break;
            case "permissionRevision":
                permissionRevision = " ";
                break;
            case "clientVersion":
                clientVersion = " ";
                break;
            case "policyVersion":
                policyVersion = " ";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(missingPart), missingPart, message: null);
        }

        return new DataCacheKey(
            clientId,
            operationName,
            DataTransportKind.Http,
            DataAccessMode.Query,
            requestFingerprint,
            authenticationScheme,
            principalRevision,
            permissionRevision,
            PluginContributionId: null,
            clientVersion,
            policyVersion);
    }

    private static string ToConstructorParameterName(string missingPart)
    {
        return missingPart switch
        {
            "clientId" => "ClientId",
            "operationName" => "OperationName",
            "requestFingerprint" => "RequestFingerprint",
            "authenticationScheme" => "AuthenticationScheme",
            "principalRevision" => "PrincipalRevision",
            "permissionRevision" => "PermissionRevision",
            "clientVersion" => "ClientVersion",
            "policyVersion" => "PolicyVersion",
            _ => throw new ArgumentOutOfRangeException(nameof(missingPart), missingPart, message: null),
        };
    }
}
