using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using AtomUI.City.Security;
using AtomUI.City.Core.Diagnostics;

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
    public async Task CheckExecutionAsyncDoesNotAllowCommandWhenEvaluatorCancelsBeforeReturning()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new AuthenticationStateStore();
        var provider = new InMemoryCommandAuthorizationDescriptorProvider();
        provider.Add(new CommandAuthorizationDescriptor(
            "settings.save",
            AuthorizationPolicy.RequireAuthenticated("SignedIn")));
        using var source = new CommandAuthorizationSource(
            new CancellingAuthorizationEvaluator(cancellation),
            store,
            provider);

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

    [Fact]
    public void ConstructorRollsBackEarlierSubscriptionsWhenLaterSubscriptionFails()
    {
        var authentication = new TrackingAuthenticationStateProvider();
        var descriptor = new TrackingCommandDescriptorProvider(throwOnAdd: true);

        Assert.Throws<InvalidOperationException>(() => new CommandAuthorizationSource(
            new AuthorizationEvaluator(new PermissionRegistry()),
            new AuthenticationStateStore(),
            descriptor,
            authentication));

        Assert.True(authentication.RemoveAttempted);
        Assert.True(descriptor.RemoveAttempted);
    }

    [Fact]
    public void FailedConstructorQuarantinesSourceWhenSubscriptionRollbackAlsoFails()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var descriptor = new TrackingCommandDescriptorProvider(
            throwOnAdd: true,
            throwOnRemove: true);

        var exception = Assert.Throws<AggregateException>(() => new CommandAuthorizationSource(
            new AuthorizationEvaluator(new PermissionRegistry()),
            new AuthenticationStateStore(),
            descriptor,
            authenticationStateProvider: null,
            permissionRegistry: null,
            diagnostics: diagnostics));
        descriptor.RaiseChanged();

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.True(descriptor.RemoveAttempted);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == SecurityDiagnosticIds.CommandAuthorizationFailed
                && record.Context["operation"] == "Subscribe");
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == SecurityDiagnosticIds.CommandAuthorizationFailed
                && record.Context["operation"] == "SubscribeRollback");
        Assert.DoesNotContain(
            diagnostics.Records,
            record => record.Code == SecurityDiagnosticIds.CommandAuthorizationChanged);
    }

    [Fact]
    public void DisposeAttemptsEveryUnsubscriptionAndAggregatesFailures()
    {
        var authentication = new TrackingAuthenticationStateProvider(throwOnRemove: true);
        var descriptor = new TrackingCommandDescriptorProvider();
        var permissions = new TrackingPermissionRegistry();
        var source = new CommandAuthorizationSource(
            new AuthorizationEvaluator(new PermissionRegistry()),
            new AuthenticationStateStore(),
            descriptor,
            authentication,
            permissions);

        var exception = Assert.Throws<AggregateException>(source.Dispose);

        Assert.Single(exception.InnerExceptions);
        Assert.True(authentication.RemoveAttempted);
        Assert.True(descriptor.RemoveAttempted);
        Assert.True(permissions.RemoveAttempted);
        source.Dispose();
    }

    [Fact]
    public void DescriptorContributionRevocationRemovesCommandsAndRejectsReregistration()
    {
        var provider = new InMemoryCommandAuthorizationDescriptorProvider();
        provider.Add(new CommandAuthorizationDescriptor(
            "sales.export",
            AuthorizationPolicy.RequireAuthenticated("SignedIn"),
            contributionId: "SalesPlugin"));

        var removed = provider.RemoveByContribution("SalesPlugin");
        var added = provider.Add(new CommandAuthorizationDescriptor(
            "sales.import",
            AuthorizationPolicy.RequireAuthenticated("SignedIn"),
            contributionId: "SalesPlugin"));

        Assert.Equal(1, removed);
        Assert.False(added);
    }

    [Fact]
    public void DescriptorInheritsPolicyContributionForRevocation()
    {
        var provider = new InMemoryCommandAuthorizationDescriptorProvider();
        var policy = AuthorizationPolicy.RequirePermission(
            "PluginPolicy",
            "plugin.sales.export",
            contributionId: "SalesPlugin");
        var descriptor = new CommandAuthorizationDescriptor("sales.export", policy);

        Assert.Equal("SalesPlugin", descriptor.ContributionId);
        Assert.True(provider.Add(descriptor));
        Assert.Equal(1, provider.RemoveByContribution("SalesPlugin"));
    }

    [Fact]
    public void DescriptorRejectsAContributionThatConflictsWithItsPolicy()
    {
        var policy = AuthorizationPolicy.RequirePermission(
            "PluginPolicy",
            "plugin.sales.export",
            contributionId: "SalesPlugin");

        Assert.Throws<ArgumentException>(() => new CommandAuthorizationDescriptor(
            "sales.export",
            policy,
            contributionId: "OtherPlugin"));
    }

    [Fact]
    public async Task UnexpectedOperationCancelledExceptionBecomesFailedCommandState()
    {
        var exception = new OperationCanceledException("provider failed without caller cancellation");
        var source = CreateSource(provider: new ThrowingCommandAuthorizationDescriptorProvider(exception));

        var state = await source.GetStateAsync(new CommandAuthorizationContext("settings.save"));

        Assert.Equal(AuthorizationResultStatus.Failed, state.Authorization.Status);
        Assert.Same(exception, state.Authorization.Exception);
    }

    [Fact]
    public void AuthorizationObserverFailureIsIsolatedAndDiagnosed()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var store = new AuthenticationStateStore();
        var permissions = new PermissionRegistry();
        var provider = new InMemoryCommandAuthorizationDescriptorProvider();
        using var source = new CommandAuthorizationSource(
            new AuthorizationEvaluator(permissions),
            store,
            provider,
            store,
            permissions,
            diagnostics);
        var secondObserverCalled = false;
        source.AuthorizationChanged += (_, _) => throw new InvalidOperationException("observer failed");
        source.AuthorizationChanged += (_, _) => secondObserverCalled = true;

        store.SetAuthenticated(CreatePrincipal(permissions: []));

        Assert.True(secondObserverCalled);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == SecurityDiagnosticIds.CommandAuthorizationObserverFailed);
    }

    [Fact]
    public async Task CommandAuthorizationWritesCommandPolicyAndResultDiagnostic()
    {
        var diagnostics = new InMemoryHostDiagnostics();
        var store = new AuthenticationStateStore();
        var permissions = new PermissionRegistry();
        var provider = new InMemoryCommandAuthorizationDescriptorProvider();
        provider.Add(new CommandAuthorizationDescriptor(
            "settings.open",
            AuthorizationPolicy.RequireAuthenticated("SignedIn")));
        using var source = new CommandAuthorizationSource(
            new AuthorizationEvaluator(permissions),
            store,
            provider,
            store,
            permissions,
            diagnostics);

        var state = await source.GetStateAsync(new CommandAuthorizationContext("settings.open"));

        Assert.False(state.CanExecute);
        var record = Assert.Single(
            diagnostics.Records,
            item => item.Code == SecurityDiagnosticIds.CommandAuthorizationEvaluated);
        Assert.Equal("settings.open", record.Context["commandId"]);
        Assert.Equal("SignedIn", record.Context["policyName"]);
        Assert.Equal("Challenge", record.Context["resultStatus"]);
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
            identity.AddClaim(new Claim(SecurityClaimTypes.Permission, permission));
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

    private sealed class TrackingAuthenticationStateProvider(
        bool throwOnRemove = false) : IAuthenticationStateProvider
    {
        public AuthenticationStateSnapshot Current { get; } = new(
            AuthenticationState.Unknown,
            SecurityPrincipals.Anonymous,
            revision: 0);

        public bool RemoveAttempted { get; private set; }

        public event EventHandler<AuthenticationStateChangedEventArgs>? StateChanged
        {
            add { }
            remove
            {
                RemoveAttempted = true;
                if (throwOnRemove)
                {
                    throw new InvalidOperationException("authentication unsubscribe failed");
                }
            }
        }
    }

    private sealed class TrackingCommandDescriptorProvider(
        bool throwOnAdd = false,
        bool throwOnRemove = false) : ICommandAuthorizationDescriptorProvider
    {
        private EventHandler<CommandAuthorizationChangedEventArgs>? _descriptorChanged;

        public bool RemoveAttempted { get; private set; }

        public event EventHandler<CommandAuthorizationChangedEventArgs>? DescriptorChanged
        {
            add
            {
                _descriptorChanged += value;
                if (throwOnAdd)
                {
                    throw new InvalidOperationException("descriptor subscribe failed");
                }
            }
            remove
            {
                RemoveAttempted = true;
                if (throwOnRemove)
                {
                    throw new InvalidOperationException("descriptor unsubscribe failed");
                }

                _descriptorChanged -= value;
            }
        }

        public void RaiseChanged()
        {
            _descriptorChanged?.Invoke(
                this,
                new CommandAuthorizationChangedEventArgs(
                    CommandAuthorizationChangeReason.DescriptorChanged,
                    revision: 1));
        }

        public ValueTask<CommandAuthorizationDescriptor?> GetDescriptorAsync(
            CommandAuthorizationContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<CommandAuthorizationDescriptor?>(null);
        }
    }

    private sealed class TrackingPermissionRegistry : IPermissionRegistry
    {
        public bool RemoveAttempted { get; private set; }

        public event EventHandler<PermissionRegistryChangedEventArgs>? Changed
        {
            add { }
            remove => RemoveAttempted = true;
        }

        public long Revision => 0;

        public IReadOnlyCollection<PermissionDescriptor> Permissions => [];

        public bool Contains(string name)
        {
            return false;
        }

        public bool TryGet(
            string name,
            [NotNullWhen(true)] out PermissionDescriptor? descriptor)
        {
            descriptor = null;
            return false;
        }
    }

    private sealed class CancellingAuthorizationEvaluator(
        CancellationTokenSource cancellation) : IAuthorizationEvaluator
    {
        public ValueTask<AuthorizationResult> EvaluateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromResult(AuthorizationResult.Allowed());
        }

        public ValueTask<AuthorizationResult> EvaluatePolicyAsync(
            ClaimsPrincipal? principal,
            string policyName,
            string? resourceName = null,
            string? contributionId = null,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromResult(AuthorizationResult.Allowed());
        }
    }
}
