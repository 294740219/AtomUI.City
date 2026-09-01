using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Core.Tests;

public sealed class ApplicationHostBuilderTests
{
    [Fact]
    public void CreateBuilderCreatesConfigurableApplicationHostBuilder()
    {
        var builder = ApplicationHost.CreateBuilder(["--sample:enabled=true"]);

        Assert.IsAssignableFrom<IApplicationHostBuilder>(builder);
        Assert.NotNull(builder.Configuration);
        Assert.Equal("true", builder.Configuration["sample:enabled"]);
    }

    [Fact]
    public void BuilderDoesNotExposeMutableServiceCollectionOrProperties()
    {
        Assert.Null(typeof(IApplicationHostBuilder).GetProperty("Services"));
        Assert.Null(typeof(ApplicationHostBuilder).GetProperty("Services"));
        Assert.Null(typeof(IApplicationHostBuilder).GetProperty("Properties"));
        Assert.Null(typeof(ApplicationHostBuilder).GetProperty("Properties"));
    }

    [Fact]
    public async Task BuildCreatesApplicationHostWithServicesAndContext()
    {
        var builder = ApplicationHostTestBuilder.Create(["--mode=test"]);

        builder.ConfigureServices(services => services.AddSingleton<TestService>());

        await using var host = builder.Build();

        Assert.IsAssignableFrom<IApplicationHost>(host);
        Assert.IsType<TestService>(host.Services.GetRequiredService<TestService>());
        Assert.Equal(["--mode=test"], host.Context.StartupArguments);
        Assert.Equal("test", host.Services.GetRequiredService<IConfiguration>()["mode"]);
        Assert.Equal(ApplicationHostTestBuilder.ApplicationId, host.Context.ApplicationId);
        Assert.Equal(ApplicationHostTestBuilder.ApplicationName, host.Context.ApplicationName);
        Assert.NotEqual(Guid.Empty, host.Context.ApplicationInstanceId);
        Assert.False(string.IsNullOrWhiteSpace(host.Context.ApplicationVersion));
        Assert.True(Path.IsPathFullyQualified(host.Context.ContentRootPath));
    }

    [Fact]
    public async Task ConfigureServicesRunsAfterAllModuleServiceStagesInRegistrationOrder()
    {
        ServiceOrderingModule.Reset();
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<ServiceOrderingModule>();
        builder.ConfigureServices(services =>
        {
            ServiceOrderingModule.Record("UserServices:First");
            services.AddSingleton<IOrderedService, UserOrderedService>();
        });
        builder.ConfigureServices(_ => ServiceOrderingModule.Record("UserServices:Second"));

        Assert.Empty(ServiceOrderingModule.Calls);

        await using var host = builder.Build();

        Assert.Equal(
            [
                "Module:PreConfigureServices",
                "Module:ConfigureServices",
                "Module:PostConfigureServices",
                "UserServices:First",
                "UserServices:Second",
            ],
            ServiceOrderingModule.Calls);
        Assert.IsType<UserOrderedService>(host.Services.GetRequiredService<IOrderedService>());
        Assert.Collection(
            host.Services.GetServices<IOrderedService>(),
            service => Assert.IsType<ModuleOrderedService>(service),
            service => Assert.IsType<UserOrderedService>(service));
    }

    [Fact]
    public async Task ConfigureServicesCanRemoveAndReplaceModuleDefaults()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<ReplaceableServiceModule>();
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRemovableService>();
            services.Replace(ServiceDescriptor.Singleton<IReplaceableService, UserReplaceableService>());
        });

        await using var host = builder.Build();

        Assert.Null(host.Services.GetService<IRemovableService>());
        Assert.IsType<UserReplaceableService>(host.Services.GetRequiredService<IReplaceableService>());
        Assert.Single(host.Services.GetServices<IReplaceableService>());
    }

    [Fact]
    public async Task UserServiceCollectionCapturedByCallbackIsFrozenAfterBuild()
    {
        IServiceCollection? capturedServices = null;
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureServices(services =>
        {
            capturedServices = services;
            services.AddSingleton<TestService>();
        });

        await using var host = builder.Build();

        Assert.NotNull(capturedServices);
        Assert.True(capturedServices.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => capturedServices.AddSingleton<LateService>());
    }

    [Fact]
    public void UserServiceFailureIsDiagnosedAndFreezesBuilder()
    {
        IServiceCollection? capturedServices = null;
        var builder = ApplicationHostTestBuilder.Create();
        var diagnostics = builder.GetBuildDiagnostics();
        builder.ConfigureServices(services =>
        {
            capturedServices = services;
            throw new InvalidOperationException("user service configuration failed");
        });

        var failure = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("user service configuration failed", failure.Message);
        Assert.NotNull(capturedServices);
        Assert.True(capturedServices.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => capturedServices.AddSingleton<LateService>());
        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureServices(services => services.AddSingleton<LateService>()));
        Assert.Contains(diagnostics.Records, record =>
            record.Code == HostDiagnosticIds.HostBuildFailed &&
            record.Context["stage"] == "UserServices");
    }

    [Fact]
    public void ModuleServiceFailureSkipsUserServiceCallbacks()
    {
        var userServicesCalled = false;
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<FailingServiceModule>();
        builder.ConfigureServices(_ => userServicesCalled = true);

        Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.False(userServicesCalled);
    }

    [Fact]
    public async Task StartupArgumentsRejectExternalListMutation()
    {
        await using var host = ApplicationHostTestBuilder.Create(["--mode=test"]).Build();
        var arguments = Assert.IsAssignableFrom<IList<string>>(host.Context.StartupArguments);

        Assert.Throws<NotSupportedException>(() => arguments[0] = "--mode=changed");
        Assert.Equal("--mode=test", host.Context.StartupArguments[0]);
    }

    [Fact]
    public async Task StartupArgumentsAreCopiedBeforeBuild()
    {
        var source = new[] { "--mode=original" };
        var builder = ApplicationHostTestBuilder.Create(source);
        source[0] = "--mode=changed";

        await using var host = builder.Build();

        Assert.Equal("--mode=original", host.Context.StartupArguments[0]);
    }

    [Fact]
    public void ApplicationContextPublicContractContainsOnlyImmutableDescriptorFields()
    {
        var propertyNames = typeof(IApplicationContext)
            .GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "AppDataPath",
                "ApplicationId",
                "ApplicationInstanceId",
                "ApplicationName",
                "ApplicationVersion",
                "ContentRootPath",
                "EnvironmentName",
                "StartupArguments",
            },
            propertyNames);
        Assert.All(typeof(IApplicationContext).GetProperties(), property => Assert.False(property.CanWrite));
    }

    [Fact]
    public async Task BuildFreezesPublicBuilderMutationEntrypoints()
    {
        var builder = ApplicationHostTestBuilder.Create();

        await using var host = builder.Build();

        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureServices(services => services.AddSingleton<TestService>()));
        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureHost(options => options.ApplicationName = "changed"));
        Assert.Throws<InvalidOperationException>(() =>
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["changed"] = "true" }));
    }

    [Fact]
    public async Task BuildCanOnlyRunOnce()
    {
        var builder = ApplicationHostTestBuilder.Create();

        await using var host = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("only build once", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildFreezesModuleAndLifecycleExtensionStores()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.UseModule<LateModule>();
        builder.UseModule<LateModule>();

        await using var host = builder.Build();

        Assert.Single(
            host.Services.GetRequiredService<IModuleRegistry>().Modules,
            descriptor => descriptor.ModuleType == typeof(LateModule));
        Assert.Throws<InvalidOperationException>(() => builder.UseModule<LateModule>());
        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureLifecycle(lifecycle =>
                lifecycle.Use(static (_, next) => next())));
    }

    [Fact]
    public void BuildFailureKeepsModuleAndLifecycleExtensionStoresFrozen()
    {
        var builder = ApplicationHostTestBuilder.Create();
        builder.ConfigureHost(_ => builder.UseModule<LateModule>());

        var failure = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("frozen after Build", failure.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => builder.UseModule<LateModule>());
        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureLifecycle(static _ => { }));
    }

    private interface IOrderedService;

    private interface IRemovableService;

    private interface IReplaceableService;

    private sealed class TestService;

    private sealed class LateService;

    private sealed class ModuleOrderedService : IOrderedService;

    private sealed class UserOrderedService : IOrderedService;

    private sealed class ModuleRemovableService : IRemovableService;

    private sealed class ModuleReplaceableService : IReplaceableService;

    private sealed class UserReplaceableService : IReplaceableService;

    private sealed class LateModule : ModuleBase;

    private sealed class ServiceOrderingModule : ModuleBase
    {
        private static readonly List<string> RecordedCalls = [];

        public static IReadOnlyList<string> Calls => RecordedCalls;

        public static void Reset() => RecordedCalls.Clear();

        public static void Record(string call) => RecordedCalls.Add(call);

        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            Record("Module:PreConfigureServices");
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Record("Module:ConfigureServices");
            context.Services.AddSingleton<IOrderedService, ModuleOrderedService>();
        }

        public override void PostConfigureServices(ServiceConfigurationContext context)
        {
            Record("Module:PostConfigureServices");
        }
    }

    private sealed class ReplaceableServiceModule : ModuleBase
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<IRemovableService, ModuleRemovableService>();
            context.Services.AddSingleton<IReplaceableService, ModuleReplaceableService>();
        }
    }

    private sealed class FailingServiceModule : ModuleBase
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            throw new InvalidOperationException("module service configuration failed");
        }
    }
}
