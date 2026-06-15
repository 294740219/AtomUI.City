using AtomUI.City.Security;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Security.Tests;

public sealed class SecurityRegistrationTests
{
    [Fact]
    public void AddSecurityRegistersCoreServices()
    {
        var services = new ServiceCollection();

        services.AddSecurity();

        using var serviceProvider = services.BuildServiceProvider();
        var stateProvider = serviceProvider.GetRequiredService<IAuthenticationStateProvider>();
        var principalAccessor = serviceProvider.GetRequiredService<ICurrentPrincipalAccessor>();
        var registry = serviceProvider.GetRequiredService<IPermissionRegistry>();
        var policyProvider = serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var permissionChecker = serviceProvider.GetRequiredService<IPermissionChecker>();
        var evaluator = serviceProvider.GetRequiredService<IAuthorizationEvaluator>();
        var commandDescriptorProvider = serviceProvider.GetRequiredService<ICommandAuthorizationDescriptorProvider>();
        var commandAuthorizationSource = serviceProvider.GetRequiredService<ICommandAuthorizationSource>();
        var tokenProvider = serviceProvider.GetRequiredService<IAccessTokenProvider>();

        Assert.Same(stateProvider, principalAccessor);
        Assert.IsType<PermissionRegistry>(registry);
        Assert.IsType<InMemoryAuthorizationPolicyProvider>(policyProvider);
        Assert.IsType<PermissionChecker>(permissionChecker);
        Assert.IsType<AuthorizationEvaluator>(evaluator);
        Assert.IsType<InMemoryCommandAuthorizationDescriptorProvider>(commandDescriptorProvider);
        Assert.IsType<CommandAuthorizationSource>(commandAuthorizationSource);
        Assert.IsType<UnavailableAccessTokenProvider>(tokenProvider);
    }

    [Fact]
    public async Task AddSecurityDefaultAccessTokenProviderReturnsUnavailableResult()
    {
        var services = new ServiceCollection();
        services.AddSecurity();

        using var serviceProvider = services.BuildServiceProvider();
        var tokenProvider = serviceProvider.GetRequiredService<IAccessTokenProvider>();

        var result = await tokenProvider.GetTokenAsync(new AccessTokenRequest("catalog"));

        Assert.Equal(AccessTokenResultStatus.Unavailable, result.Status);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DelegateAccessTokenProviderReturnsSuccessResult()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var provider = new DelegateAccessTokenProvider((request, _) =>
        {
            Assert.Equal("catalog", request.ResourceName);
            Assert.Equal("Bearer", request.Scheme);
            Assert.Equal("secure-items", request.OperationName);
            return ValueTask.FromResult(AccessTokenResult.Success("access-token", "Bearer", expiresAt));
        });

        var result = await provider.GetTokenAsync(new AccessTokenRequest("catalog", "Bearer", "secure-items"));

        Assert.Equal(AccessTokenResultStatus.Success, result.Status);
        Assert.Equal("access-token", result.Token);
        Assert.Equal("Bearer", result.Scheme);
        Assert.Equal(expiresAt, result.ExpiresAt);
    }

    [Fact]
    public async Task DelegateAccessTokenProviderMapsExceptionToFailedResult()
    {
        var exception = new InvalidOperationException("Token refresh failed.");
        var provider = new DelegateAccessTokenProvider((_, _) => throw exception);

        var result = await provider.GetTokenAsync(new AccessTokenRequest("catalog"));

        Assert.Equal(AccessTokenResultStatus.Failed, result.Status);
        Assert.False(result.Succeeded);
        Assert.Equal("Token refresh failed.", result.Message);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task DelegateAccessTokenProviderReturnsCancelledWithoutCallingDelegate()
    {
        var called = false;
        var provider = new DelegateAccessTokenProvider((_, _) =>
        {
            called = true;
            return ValueTask.FromResult(AccessTokenResult.Success("access-token", "Bearer"));
        });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await provider.GetTokenAsync(new AccessTokenRequest("catalog"), cancellation.Token);

        Assert.Equal(AccessTokenResultStatus.Cancelled, result.Status);
        Assert.False(called);
    }

    [Fact]
    public async Task UnavailableAccessTokenProviderObservesCancellation()
    {
        var provider = new UnavailableAccessTokenProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await provider.GetTokenAsync(new AccessTokenRequest("catalog"), cancellation.Token);

        Assert.Equal(AccessTokenResultStatus.Cancelled, result.Status);
    }
}
