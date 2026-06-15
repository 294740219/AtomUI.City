using System.Globalization;
using AtomUI.City.Localization;

namespace AtomUI.City.Localization.Tests;

public sealed class CultureStateTests
{
    [Fact]
    public void LocalizationOptionsDefaultCultureInitializesServiceState()
    {
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("en-US"),
            DefaultUICulture = CultureInfo.GetCultureInfo("en-US"),
        };
        options.FallbackCultures.Add(CultureInfo.GetCultureInfo("ja-JP"));
        var service = new LocalizationService(options, []);

        Assert.Equal("en-US", service.CurrentCulture.Name);
        Assert.Equal("en-US", service.State.CurrentUICulture.Name);
        Assert.Equal(0, service.CultureRevision);
        Assert.Equal(
            ["ja-JP", "en", ""],
            service.State.FallbackCultures.Select(culture => culture.Name).ToArray());
        Assert.Empty(service.State.LoadedPackageIds);
    }

    [Fact]
    public async Task SetCulturePublishesFallbackChainInStableOrder()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var zh = LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>());
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            diagnostics: diagnostics);

        var result = await service.SetCultureAsync("zh-CN");

        Assert.True(result.Succeeded);
        Assert.Equal(
            ["en-US", "zh-Hans", "zh", ""],
            service.State.FallbackCultures.Select(culture => culture.Name).ToArray());
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.CultureChanged
                && record.CultureName == "zh-CN"
                && record.FallbackCultureName == "en-US;zh-Hans;zh;");
    }

    [Fact]
    public async Task SetCultureRejectsInvalidCultureWithoutChangingState()
    {
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("en-US"),
            DefaultUICulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var service = new LocalizationService(
            options,
            [],
            diagnostics: diagnostics);

        var result = await service.SetCultureAsync("not a culture");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.InvalidCulture, result.Error?.Kind);
        Assert.Equal("en-US", service.CurrentCulture.Name);
        Assert.Equal(0, service.CultureRevision);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.CultureSwitchRejected
                && record.CultureName == "not a culture"
                && record.ErrorKind == LocalizationErrorKind.InvalidCulture);
    }

    [Fact]
    public async Task SetCultureSkipsRepeatedCultureWithoutReloadingPackages()
    {
        var zh = Package("Host.zh-CN", "zh-CN");
        var provider = new RecordingLanguagePackageProvider(zh);
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor],
            [provider],
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        var revision = service.CultureRevision;
        var result = await service.SetCultureAsync("zh-CN");

        Assert.True(result.Succeeded);
        Assert.Equal(revision, service.CultureRevision);
        Assert.Equal(["zh-CN"], provider.LoadedCultures);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.CultureSwitchSkipped
                && record.CultureName == "zh-CN");
    }

    [Fact]
    public async Task SetCultureRejectsFallbackCycleWithoutChangingState()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        var zh = LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>());
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            diagnostics: diagnostics);

        var result = await service.SetCultureAsync("zh-CN");

        Assert.False(result.Succeeded);
        Assert.Equal(LocalizationErrorKind.InvalidCulture, result.Error?.Kind);
        Assert.Equal(CultureInfo.InvariantCulture, service.CurrentCulture);
        Assert.Equal(0, service.CultureRevision);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.CultureSwitchRejected
                && record.CultureName == "zh-CN"
                && record.FallbackCultureName == "zh-CN");
    }

    [Fact]
    public void CollectionsRejectExternalListMutation()
    {
        var state = new CultureState(
            CultureInfo.GetCultureInfo("zh-CN"),
            CultureInfo.GetCultureInfo("zh-CN"),
            [CultureInfo.GetCultureInfo("en-US")],
            revision: 1,
            ["Host.zh-CN"]);
        var fallbackCultures = Assert.IsAssignableFrom<IList<CultureInfo>>(state.FallbackCultures);
        var loadedPackageIds = Assert.IsAssignableFrom<IList<string>>(state.LoadedPackageIds);

        Assert.Throws<NotSupportedException>(() => fallbackCultures[0] = CultureInfo.GetCultureInfo("ja-JP"));
        Assert.Throws<NotSupportedException>(() => loadedPackageIds[0] = "Changed");
        Assert.Equal("en-US", state.FallbackCultures[0].Name);
        Assert.Equal("Host.zh-CN", state.LoadedPackageIds[0]);
    }

    private static LanguagePackage Package(
        string packageId,
        string cultureName,
        params (string Key, string Value)[] resources)
    {
        return LanguagePackage.Create(
            new LanguagePackageDescriptor(
                packageId,
                CultureInfo.GetCultureInfo(cultureName),
                ResourceScope.Host),
            resources.ToDictionary(resource => resource.Key, resource => resource.Value));
    }
}
