using System.Globalization;
using AtomUI.City.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Localization.Tests;

public sealed class LanguagePackageProviderTests
{
    [Fact]
    public void AddLocalizationRegistersLanguagePackageRegistry()
    {
        var services = new ServiceCollection();

        services.AddLocalization();

        using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<LanguagePackageRegistry>();

        Assert.Empty(registry.Registrations);
    }

    [Fact]
    public void RegistryRegistersDescriptorsWithOwners()
    {
        var registry = new LanguagePackageRegistry();
        var descriptor = Descriptor("Host.zh-CN", "zh-CN");

        var result = registry.Register(descriptor, "host");

        Assert.True(result.Succeeded);
        var registration = Assert.Single(registry.Registrations);
        Assert.Same(descriptor, registration.Descriptor);
        Assert.Equal("host", registration.OwnerId);
    }

    [Fact]
    public void RegistryRejectsDuplicatePackageIds()
    {
        var registry = new LanguagePackageRegistry();
        var descriptor = Descriptor("Host.zh-CN", "zh-CN");

        registry.Register(descriptor, "host");
        var result = registry.Register(descriptor, "module.settings");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageAlreadyRegistered, result.Error?.Kind);
        Assert.Single(registry.Registrations);
    }

    [Fact]
    public void RegistryRevokesOwnerDescriptorsAndRejectsFutureRegistrations()
    {
        var registry = new LanguagePackageRegistry();
        var descriptor = Descriptor("Plugin.sales.zh-CN", "zh-CN");

        registry.Register(descriptor, "plugin.sales");
        var revokedCount = registry.RevokeOwner("plugin.sales");
        var result = registry.Register(descriptor, "plugin.sales");

        Assert.Equal(1, revokedCount);
        Assert.Empty(registry.Registrations);
        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.OwnerRevoked, result.Error?.Kind);
    }

    [Fact]
    public async Task FileProviderLoadsLocPackForRequestedCulture()
    {
        using var workspace = new LocalizationTestWorkspace();
        var locpackPath = workspace.WriteLocPack(
            """
            {
              "packageId": "Host.zh-CN",
              "culture": "zh-CN",
              "resources": {
                "Settings.Title": "Settings zh"
              }
            }
            """);
        var descriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = locpackPath,
        };
        var provider = new FileLanguagePackageProvider();

        var result = await provider.LoadAsync(descriptor);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Package);
        Assert.True(result.Package.TryGetString("Settings.Title", out var value));
        Assert.Equal("Settings zh", value);
    }

    [Fact]
    public async Task FileProviderReturnsCancelledResultWhenTokenIsCancelled()
    {
        using var workspace = new LocalizationTestWorkspace();
        var locpackPath = workspace.WriteLocPack(
            """
            {
              "packageId": "Host.zh-CN",
              "culture": "zh-CN",
              "resources": {}
            }
            """);
        var descriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = locpackPath,
        };
        var provider = new FileLanguagePackageProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await provider.LoadAsync(descriptor, cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.Cancelled, result.Error?.Kind);
    }

    [Fact]
    public async Task AssemblyProviderLoadsEmbeddedLocPackResource()
    {
        var descriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.Assembly,
            Location = typeof(LanguagePackageProviderTests).Assembly.Location,
            ResourceBaseName = "AtomUI.City.Localization.Tests.Fixtures.Host.en-US.locpack.json",
        };
        var provider = new AssemblyLanguagePackageProvider();

        var result = await provider.LoadAsync(descriptor);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Package);
        Assert.True(result.Package.TryGetString("Settings.Title", out var value));
        Assert.Equal("Settings", value);
    }

    [Fact]
    public async Task AssemblyProviderReturnsCancelledResultWhenTokenIsCancelled()
    {
        var descriptor = new LanguagePackageDescriptor(
            "Host.en-US",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.Assembly,
            Location = typeof(LanguagePackageProviderTests).Assembly.Location,
            ResourceBaseName = "AtomUI.City.Localization.Tests.Fixtures.Host.en-US.locpack.json",
        };
        var provider = new AssemblyLanguagePackageProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await provider.LoadAsync(descriptor, cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.Cancelled, result.Error?.Kind);
    }

    [Fact]
    public async Task FileProviderRejectsCultureMismatch()
    {
        using var workspace = new LocalizationTestWorkspace();
        var locpackPath = workspace.WriteLocPack(
            """
            {
              "packageId": "Host.zh-CN",
              "culture": "en-US",
              "resources": {
                "Settings.Title": "Settings"
              }
            }
            """);
        var descriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = locpackPath,
        };
        var provider = new FileLanguagePackageProvider();

        var result = await provider.LoadAsync(descriptor);

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageCultureMismatch, result.Error?.Kind);
    }

    [Fact]
    public async Task FileProviderReturnsFailedResultForMalformedLocPack()
    {
        using var workspace = new LocalizationTestWorkspace();
        var locpackPath = workspace.WriteLocPack(
            """
            {
              "packageId": "Host.zh-CN",
              "resources": {}
            }
            """);
        var descriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            ProviderKind = LanguagePackageProviderKind.File,
            Location = locpackPath,
        };
        var provider = new FileLanguagePackageProvider();

        var result = await provider.LoadAsync(descriptor);

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.PackageLoadFailed, result.Error?.Kind);
    }

    private static LanguagePackageDescriptor Descriptor(
        string packageId,
        string cultureName)
    {
        return new LanguagePackageDescriptor(
            packageId,
            CultureInfo.GetCultureInfo(cultureName),
            ResourceScope.Host);
    }
}
