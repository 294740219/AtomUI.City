using System.Security.Claims;
using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class CommandAuthorizationSourceTests
{
    [Fact]
    public async Task GetStateAsyncAllowsCommandWithoutAuthorizationDescriptor()
    {
        var source = CreateSource();

        var state = await source.GetStateAsync(new CommandAuthorizationContext("settings.open"));

        Assert.True(state.CanExecute);
        Assert.True(state.IsVisible);
        Assert.Equal(AuthorizationResultStatus.Allowed, state.Authorization.Status);
    }

    [Fact]
    public async Task GetStateAsyncDisablesProtectedCommandForAnonymousPrincipal()
    {
        var source = CreateSource(provider =>
        {
            provider.Add(new CommandAuthorizationDescriptor(
                "settings.open",
                AuthorizationPolicy.RequireAuthenticated("SignedIn")));
        });

        var state = await source.GetStateAsync(new CommandAuthorizationContext("settings.open"));

        Assert.False(state.CanExecute);
        Assert.True(state.IsVisible);
        Assert.Equal(CommandUnauthorizedBehavior.Disable, state.UnauthorizedBehavior);
        Assert.Equal(AuthorizationResultStatus.Challenge, state.Authorization.Status);
    }

    [Fact]
    public async Task GetStateAsyncHidesForbiddenCommandWhenDescriptorRequestsHidden()
    {
        var store = new AuthenticationStateStore();
        store.SetAuthenticated(CreatePrincipal(permissions: []));
        var permissions = new PermissionRegistry();
        permissions.Add(new PermissionDescriptor("settings.write"));
        var source = CreateSource(
            provider =>
            {
                provider.Add(new CommandAuthorizationDescriptor(
                    "settings.save",
                    AuthorizationPolicy.RequirePermission("CanWriteSettings", "settings.write"),
                    CommandUnauthorizedBehavior.Hide,
                    deniedMessageKey: "Permissions.Settings.WriteDenied"));
            },
            store,
            permissions);

        var state = await source.GetStateAsync(new CommandAuthorizationContext("settings.save"));

        Assert.False(state.CanExecute);
        Assert.False(state.IsVisible);
        Assert.Equal("Permissions.Settings.WriteDenied", state.DeniedMessageKey);
        Assert.Equal(AuthorizationResultStatus.Forbidden, state.Authorization.Status);
    }

    [Fact]
    public async Task GetStateAsyncAllowsCommandWhenPermissionIsGranted()
    {
        var store = new AuthenticationStateStore();
        store.SetAuthenticated(CreatePrincipal(permissions: ["settings.write"]));
        var permissions = new PermissionRegistry();
        permissions.Add(new PermissionDescriptor("settings.write"));
        var source = CreateSource(
            provider =>
            {
                provider.Add(new CommandAuthorizationDescriptor(
                    "settings.save",
                    AuthorizationPolicy.RequirePermission("CanWriteSettings", "settings.write")));
            },
            store,
            permissions);

        var state = await source.GetStateAsync(new CommandAuthorizationContext("settings.save"));

        Assert.True(state.CanExecute);
        Assert.True(state.IsVisible);
        Assert.Equal(AuthorizationResultStatus.Allowed, state.Authorization.Status);
    }

    [Fact]
    public void AuthenticationStateChangeRaisesAuthorizationChanged()
    {
        var store = new AuthenticationStateStore();
        var source = CreateSource(authenticationStateProvider: store);
        CommandAuthorizationChangedEventArgs? observed = null;
        source.AuthorizationChanged += (_, args) => observed = args;

        store.SetAuthenticated(CreatePrincipal(permissions: []));

        Assert.NotNull(observed);
        Assert.Equal(CommandAuthorizationChangeReason.AuthenticationStateChanged, observed.Reason);
        Assert.Equal(1, observed.Revision);
    }

    [Fact]
    public void PermissionRegistryChangeRaisesAuthorizationChanged()
    {
        var permissions = new PermissionRegistry();
        var source = CreateSource(permissions: permissions);
        CommandAuthorizationChangedEventArgs? observed = null;
        source.AuthorizationChanged += (_, args) => observed = args;

        permissions.Add(new PermissionDescriptor("settings.write"));

        Assert.NotNull(observed);
        Assert.Equal(CommandAuthorizationChangeReason.PermissionChanged, observed.Reason);
        Assert.Equal(1, observed.Revision);
    }

    [Fact]
    public void DescriptorChangeRaisesAuthorizationChangedForCommand()
    {
        var provider = new InMemoryCommandAuthorizationDescriptorProvider();
        var source = CreateSource(provider: provider);
        CommandAuthorizationChangedEventArgs? observed = null;
        source.AuthorizationChanged += (_, args) => observed = args;

        provider.Add(new CommandAuthorizationDescriptor(
            "settings.save",
            AuthorizationPolicy.RequireAuthenticated("SignedIn")));

        Assert.NotNull(observed);
        Assert.Equal(CommandAuthorizationChangeReason.DescriptorChanged, observed.Reason);
        Assert.Equal("settings.save", observed.CommandId);
    }

    [Fact]
    public async Task CheckExecutionAsyncReevaluatesAuthorizationBeforeExecution()
    {
        var store = new AuthenticationStateStore();
        store.SetAuthenticated(CreatePrincipal(permissions: ["settings.write"]));
        var permissions = new PermissionRegistry();
        permissions.Add(new PermissionDescriptor("settings.write"));
        var source = CreateSource(
            provider =>
            {
                provider.Add(new CommandAuthorizationDescriptor(
                    "settings.save",
                    AuthorizationPolicy.RequirePermission("CanWriteSettings", "settings.write")));
            },
            store,
            permissions);

        permissions.Remove("settings.write");

        var result = await source.CheckExecutionAsync(new CommandAuthorizationContext("settings.save"));

        Assert.False(result.Succeeded);
        Assert.Equal(AuthorizationResultStatus.Failed, result.Status);
        Assert.Equal(SecurityFailureKind.PermissionNotFound, result.FailureKind);
    }

    [Fact]
    public async Task PermissionContributionRevokeRaisesChangeAndDisablesCommand()
    {
        var store = new AuthenticationStateStore();
        store.SetAuthenticated(CreatePrincipal(permissions: ["plugin.sales.export"]));
        var permissions = new PermissionRegistry();
        permissions.Add(new PermissionDescriptor("plugin.sales.export", contributionId: "SalesPlugin"));
        var source = CreateSource(
            provider =>
            {
                provider.Add(new CommandAuthorizationDescriptor(
                    "sales.export",
                    AuthorizationPolicy.RequirePermission("CanExportSales", "plugin.sales.export"),
                    contributionId: "SalesPlugin"));
            },
            store,
            permissions);
        CommandAuthorizationChangedEventArgs? observed = null;
        source.AuthorizationChanged += (_, args) => observed = args;

        permissions.RemoveByContribution("SalesPlugin");
        var state = await source.GetStateAsync(new CommandAuthorizationContext("sales.export"));

        Assert.NotNull(observed);
        Assert.Equal(CommandAuthorizationChangeReason.PermissionChanged, observed.Reason);
        Assert.False(state.CanExecute);
        Assert.True(state.IsVisible);
        Assert.Equal(AuthorizationResultStatus.Failed, state.Authorization.Status);
        Assert.Equal(SecurityFailureKind.PermissionNotFound, state.Authorization.FailureKind);
    }

    [Fact]
    public async Task GetStateAsyncMapsDescriptorProviderExceptionToDisabledFailedState()
    {
        var exception = new InvalidOperationException("Descriptor provider failed.");
        var source = CreateSource(provider: new ThrowingCommandAuthorizationDescriptorProvider(exception));

        var state = await source.GetStateAsync(new CommandAuthorizationContext("settings.save"));

        Assert.False(state.CanExecute);
        Assert.True(state.IsVisible);
        Assert.Equal(CommandUnauthorizedBehavior.Disable, state.UnauthorizedBehavior);
        Assert.Equal(AuthorizationResultStatus.Failed, state.Authorization.Status);
        Assert.Equal(SecurityFailureKind.EvaluatorFailed, state.Authorization.FailureKind);
        Assert.Equal("settings.save", state.Authorization.FailedRequirement);
        Assert.Same(exception, state.Authorization.Exception);
    }

    [Fact]
    public async Task CheckExecutionAsyncMapsDescriptorProviderExceptionToFailedResult()
    {
        var exception = new InvalidOperationException("Descriptor provider failed.");
        var source = CreateSource(provider: new ThrowingCommandAuthorizationDescriptorProvider(exception));

        var result = await source.CheckExecutionAsync(new CommandAuthorizationContext("settings.save"));

        Assert.Equal(AuthorizationResultStatus.Failed, result.Status);
        Assert.Equal(SecurityFailureKind.EvaluatorFailed, result.FailureKind);
        Assert.Equal("settings.save", result.FailedRequirement);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task GetStateAsyncDoesNotAllowCommandWhenAuthorizationIsCancelled()
    {
        var source = CreateSource(provider =>
        {
            provider.Add(new CommandAuthorizationDescriptor(
                "settings.save",
                AuthorizationPolicy.RequireAuthenticated("SignedIn")));
        });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var state = await source.GetStateAsync(
            new CommandAuthorizationContext("settings.save"),
            cancellation.Token);

        Assert.False(state.CanExecute);
        Assert.Equal(AuthorizationResultStatus.Cancelled, state.Authorization.Status);
    }

    [Fact]
    public async Task CheckExecutionAsyncReturnsCancelledWhenAuthorizationIsCancelled()
    {
        var source = CreateSource(provider =>
        {
            provider.Add(new CommandAuthorizationDescriptor(
                "settings.save",
                AuthorizationPolicy.RequireAuthenticated("SignedIn")));
        });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await source.CheckExecutionAsync(
            new CommandAuthorizationContext("settings.save"),
            cancellation.Token);

        Assert.Equal(AuthorizationResultStatus.Cancelled, result.Status);
    }

    [Fact]
    public void DisposeReleasesAuthorizationSubscriptions()
    {
        var store = new AuthenticationStateStore();
        var permissions = new PermissionRegistry();
        var provider = new InMemoryCommandAuthorizationDescriptorProvider();
        var source = CreateSource(
            authenticationStateProvider: store,
            permissions: permissions,
            provider: provider);
        var changeCount = 0;
        source.AuthorizationChanged += (_, _) => changeCount++;

        source.Dispose();
        store.SetAuthenticated(CreatePrincipal(permissions: []));
        permissions.Add(new PermissionDescriptor("settings.write"));
        provider.Add(new CommandAuthorizationDescriptor(
            "settings.save",
            AuthorizationPolicy.RequireAuthenticated("SignedIn")));

        Assert.Equal(0, changeCount);
    }

    private static CommandAuthorizationSource CreateSource(
        Action<InMemoryCommandAuthorizationDescriptorProvider>? configureProvider = null,
        AuthenticationStateStore? authenticationStateProvider = null,
        PermissionRegistry? permissions = null,
        ICommandAuthorizationDescriptorProvider? provider = null)
    {
        if (provider is null)
        {
            var inMemoryProvider = new InMemoryCommandAuthorizationDescriptorProvider();
            configureProvider?.Invoke(inMemoryProvider);
            provider = inMemoryProvider;
        }

        authenticationStateProvider ??= new AuthenticationStateStore();
        permissions ??= new PermissionRegistry();

        return new CommandAuthorizationSource(
            new AuthorizationEvaluator(permissions),
            authenticationStateProvider,
            provider,
            permissionRegistry: permissions);
    }

    private static ClaimsPrincipal CreatePrincipal(IReadOnlyCollection<string> permissions)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim("permission", permission));
        }

        return new ClaimsPrincipal(identity);
    }

    private sealed class ThrowingCommandAuthorizationDescriptorProvider : ICommandAuthorizationDescriptorProvider
    {
        private readonly Exception _exception;

        public ThrowingCommandAuthorizationDescriptorProvider(Exception exception)
        {
            _exception = exception;
        }

        public event EventHandler<CommandAuthorizationChangedEventArgs>? DescriptorChanged
        {
            add { }
            remove { }
        }

        public ValueTask<CommandAuthorizationDescriptor?> GetDescriptorAsync(
            CommandAuthorizationContext context,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }
}
