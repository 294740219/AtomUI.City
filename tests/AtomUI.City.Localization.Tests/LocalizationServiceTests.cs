using System.Globalization;
using AtomUI.City.Localization;

namespace AtomUI.City.Localization.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public async Task SetCultureLoadsOnlySelectedCulturePackages()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings en"));
        var provider = new RecordingLanguagePackageProvider(zh, en);
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [provider],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var text = await service.GetStringAsync("Settings.Title");

        Assert.Equal("zh-CN", service.CurrentCulture.Name);
        Assert.Equal("Settings zh", text.Value);
        Assert.Equal(["zh-CN"], provider.LoadedCultures);
        Assert.DoesNotContain("en-US", provider.LoadedCultures);
    }

    [Fact]
    public async Task LookupLoadsFallbackPackageOnDemand()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var zh = LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>());
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings"));
        var provider = new RecordingLanguagePackageProvider(zh, en);
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [provider],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var text = await service.GetStringAsync("Settings.Title");

        Assert.Equal("Settings", text.Value);
        Assert.True(text.IsFallback);
        Assert.Equal("en-US", text.Culture.Name);
        Assert.Equal(["zh-CN", "en-US"], provider.LoadedCultures);
    }

    [Fact]
    public async Task ConcurrentLookupsShareInFlightPackageLoad()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var provider = new BlockingLanguagePackageProvider(zh);
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("zh-CN"),
            DefaultUICulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        options.LanguagePackages.Add(zh.Descriptor);
        var service = new LocalizationService(options, [provider]);

        var first = service.GetStringAsync("Settings.Title").AsTask();
        await provider.FirstLoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = service.GetStringAsync("Settings.Title").AsTask();

        await Task.Delay(50);
        Assert.Equal(1, provider.LoadCount);

        provider.Release();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal("Settings zh", result.Value));
        Assert.Equal(1, provider.LoadCount);
    }

    [Fact]
    public async Task LookupLoadFailureFallsBackAndWritesDiagnostic()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.zh-CN",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var zh = LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>());
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings"));
        var provider = new RecordingLanguagePackageProvider(zh, en)
        {
            FailingCultureName = "zh-CN",
        };
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("zh-CN"),
            DefaultUICulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        options.LanguagePackages.Add(zh.Descriptor);
        options.LanguagePackages.Add(en.Descriptor);
        var service = new LocalizationService(
            options,
            [provider],
            diagnostics: diagnostics);

        var text = await service.GetStringAsync("Settings.Title");

        Assert.Equal("Settings", text.Value);
        Assert.True(text.IsFallback);
        Assert.Equal("en-US", text.Culture.Name);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.PackageLoadFailed
                && record.PackageId == "Host.zh-CN"
                && record.CultureName == "zh-CN"
                && record.ErrorKind == LocalizationErrorKind.PackageLoadFailed);
    }

    [Fact]
    public async Task FallbackPackageCacheIsIsolatedByCulture()
    {
        var zhDescriptor = new LanguagePackageDescriptor(
            "Host.Shared",
            CultureInfo.GetCultureInfo("zh-CN"),
            ResourceScope.Host)
        {
            FallbackCulture = CultureInfo.GetCultureInfo("en-US"),
        };
        var enDescriptor = new LanguagePackageDescriptor(
            "Host.Shared",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Host);
        var zh = LanguagePackage.Create(zhDescriptor, new Dictionary<string, string>());
        var en = LanguagePackage.Create(
            enDescriptor,
            new Dictionary<string, string>
            {
                ["Settings.Title"] = "Settings",
            });
        var provider = new CultureKeyedLanguagePackageProvider(zh, en);
        var options = new LocalizationOptions
        {
            DefaultCulture = CultureInfo.GetCultureInfo("zh-CN"),
            DefaultUICulture = CultureInfo.GetCultureInfo("zh-CN"),
        };
        options.LanguagePackages.Add(zh.Descriptor);
        options.LanguagePackages.Add(en.Descriptor);
        var service = new LocalizationService(options, [provider]);

        var text = await service.GetStringAsync("Settings.Title");

        Assert.Equal("Settings", text.Value);
        Assert.True(text.IsFallback);
        Assert.Equal("en-US", text.Culture.Name);
        Assert.Equal(["zh-CN", "en-US"], provider.LoadedCultures);
    }

    [Fact]
    public async Task MissingResourceReturnsMarkerAndDiagnostic()
    {
        var zh = Package("Host.zh-CN", "zh-CN");
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            bridge: new RecordingPresentationLocalizationBridge(),
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        var text = await service.GetStringAsync("Settings.Missing");

        Assert.Equal("!Settings.Missing!", text.Value);
        Assert.True(text.IsMissing);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.ResourceMissing
                && record.ResourceKey == "Settings.Missing"
                && record.CultureName == "zh-CN");
    }

    [Fact]
    public async Task CultureSwitchRollsBackWhenPackageLoadFails()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var ja = Package("Host.ja-JP", "ja-JP", ("Settings.Title", "Settings ja"));
        var provider = new RecordingLanguagePackageProvider(zh, ja)
        {
            FailingCultureName = "ja-JP",
        };
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor, ja.Descriptor],
            [provider],
            bridge: new RecordingPresentationLocalizationBridge(),
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        var result = await service.SetCultureAsync("ja-JP");

        Assert.False(result.Succeeded);
        Assert.Equal("zh-CN", service.CurrentCulture.Name);
        Assert.Contains(diagnostics.Records, record => record.Code == LocalizationDiagnosticIds.PackageLoadFailed);
    }

    [Fact]
    public async Task CultureSwitchRollsBackWhenPresentationBridgeFails()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var ja = Package("Host.ja-JP", "ja-JP", ("Settings.Title", "Settings ja"));
        var bridge = new RecordingPresentationLocalizationBridge
        {
            FailingCultureName = "ja-JP",
        };
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor, ja.Descriptor],
            [new RecordingLanguagePackageProvider(zh, ja)],
            bridge: bridge,
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        var result = await service.SetCultureAsync("ja-JP");

        Assert.False(result.Succeeded);
        Assert.Equal("zh-CN", service.CurrentCulture.Name);
        Assert.Contains(diagnostics.Records, record => record.Code == LocalizationDiagnosticIds.AtomUiApplyFailed);
    }

    [Fact]
    public async Task SuccessfulCultureSwitchAppliesPresentationBridge()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "Settings zh"));
        var bridge = new RecordingPresentationLocalizationBridge();
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            bridge: bridge);

        await service.SetCultureAsync("zh-CN");

        Assert.Single(bridge.AppliedCultures);
        Assert.Equal("zh-CN", bridge.AppliedCultures.Single());
        Assert.Equal(1, service.CultureRevision);
    }

    [Fact]
    public async Task GetMessageAsyncFormatsMessageWithCurrentCulture()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Errors.Upload.Size", "文件大小不能超过 {0:N1} MB"));
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var message = await service.GetMessageAsync("Errors.Upload.Size", [12.345]);

        Assert.Equal("文件大小不能超过 12.3 MB", message.Value);
        Assert.Equal("Errors.Upload.Size", message.Key);
        Assert.Equal("zh-CN", message.Culture.Name);
        Assert.False(message.IsMissing);
        Assert.False(message.IsFormatFailed);
    }

    [Fact]
    public async Task GetMessageAsyncReturnsMissingMarkerWhenMessageKeyIsMissing()
    {
        var zh = Package("Host.zh-CN", "zh-CN");
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var message = await service.GetMessageAsync("Errors.Missing", ["value"]);

        Assert.Equal("!Errors.Missing!", message.Value);
        Assert.True(message.IsMissing);
        Assert.False(message.IsFormatFailed);
    }

    [Fact]
    public async Task GetMessageAsyncReturnsTemplateAndDiagnosticWhenFormatFails()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Errors.Range", "Value must be between {0} and {1}."));
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor],
            [new RecordingLanguagePackageProvider(zh)],
            bridge: new RecordingPresentationLocalizationBridge(),
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        var message = await service.GetMessageAsync("Errors.Range", [1]);

        Assert.Equal("Value must be between {0} and {1}.", message.Value);
        Assert.True(message.IsFormatFailed);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.MessageFormatFailed
                && record.ResourceKey == "Errors.Range"
                && record.ErrorKind == LocalizationErrorKind.FormatFailed);
    }

    [Fact]
    public async Task LocalizedTextRefreshesWhenCultureChanges()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "设置"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings"));
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        using var text = await service.CreateTextAsync("Settings.Title");
        var changes = new List<string>();
        text.Changed += (_, args) => changes.Add(args.Value);

        await service.SetCultureAsync("en-US");

        Assert.Equal("Settings", text.Value);
        Assert.Equal("en-US", text.Culture.Name);
        Assert.Equal(2, text.Revision);
        Assert.Equal(["Settings"], changes);
    }

    [Fact]
    public async Task LocalizedMessageTextRefreshesFormattedValueWhenCultureChanges()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Validation.Length", "字段 {0} 不能超过 {1} 个字符"));
        var en = Package("Host.en-US", "en-US", ("Validation.Length", "{0} must be at most {1} characters."));
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        using var text = await service.CreateMessageTextAsync("Validation.Length", ["Name", 12]);
        var changes = new List<string>();
        text.Changed += (_, args) => changes.Add(args.Value);

        await service.SetCultureAsync("en-US");

        Assert.Equal("Name must be at most 12 characters.", text.Value);
        Assert.Equal("en-US", text.Culture.Name);
        Assert.Equal(2, text.Revision);
        Assert.Equal(["Name must be at most 12 characters."], changes);
    }

    [Fact]
    public async Task DisposedLocalizedMessageTextDoesNotRefreshAfterCultureChanges()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Validation.Required", "请输入 {0}"));
        var en = Package("Host.en-US", "en-US", ("Validation.Required", "{0} is required."));
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var text = await service.CreateMessageTextAsync("Validation.Required", ["Name"]);
        var changed = false;
        text.Changed += (_, _) => changed = true;

        text.Dispose();
        await service.SetCultureAsync("en-US");

        Assert.False(changed);
        Assert.Equal("请输入 Name", text.Value);
        Assert.Equal(1, text.Revision);
    }

    [Fact]
    public async Task DisposedLocalizedTextDoesNotRefreshAfterCultureChanges()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "设置"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings"));
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge: new RecordingPresentationLocalizationBridge());

        await service.SetCultureAsync("zh-CN");
        var text = await service.CreateTextAsync("Settings.Title");
        var changed = false;
        text.Changed += (_, _) => changed = true;

        text.Dispose();
        await service.SetCultureAsync("en-US");

        Assert.False(changed);
        Assert.Equal("设置", text.Value);
        Assert.Equal(1, text.Revision);
    }

    [Fact]
    public async Task LocalizedTextRefreshFailureIsDiagnosticAndDoesNotStopOtherSubscribers()
    {
        var zh = Package("Host.zh-CN", "zh-CN", ("Settings.Title", "设置"));
        var en = Package("Host.en-US", "en-US", ("Settings.Title", "Settings"));
        var diagnostics = new InMemoryLocalizationDiagnostics();
        var service = new LocalizationService(
            [zh.Descriptor, en.Descriptor],
            [new RecordingLanguagePackageProvider(zh, en)],
            bridge: new RecordingPresentationLocalizationBridge(),
            diagnostics: diagnostics);

        await service.SetCultureAsync("zh-CN");
        using var throwingText = await service.CreateTextAsync("Settings.Title");
        using var secondText = await service.CreateTextAsync("Settings.Title");
        var secondChanged = false;
        throwingText.Changed += (_, _) => throw new InvalidOperationException("refresh failed");
        secondText.Changed += (_, _) => secondChanged = true;

        var result = await service.SetCultureAsync("en-US");

        Assert.True(result.Succeeded);
        Assert.True(secondChanged);
        Assert.Contains(
            diagnostics.Records,
            record => record.Code == LocalizationDiagnosticIds.TextRefreshFailed
                && record.ResourceKey == "Settings.Title"
                && record.ErrorKind == LocalizationErrorKind.RefreshFailed);
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

    private sealed class BlockingLanguagePackageProvider : ILanguagePackageProvider
    {
        private readonly LanguagePackage _package;
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingLanguagePackageProvider(LanguagePackage package)
        {
            _package = package;
        }

        public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.InMemory;

        public TaskCompletionSource FirstLoadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int LoadCount { get; private set; }

        public void Release()
        {
            _release.TrySetResult();
        }

        public async ValueTask<LanguagePackageLoadResult> LoadAsync(
            LanguagePackageDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            LoadCount++;
            FirstLoadStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

            return LanguagePackageLoadResult.Success(_package);
        }
    }

    private sealed class CultureKeyedLanguagePackageProvider : ILanguagePackageProvider
    {
        private readonly Dictionary<string, LanguagePackage> _packages;

        public CultureKeyedLanguagePackageProvider(params LanguagePackage[] packages)
        {
            _packages = packages.ToDictionary(
                package => GetKey(package.Descriptor),
                StringComparer.Ordinal);
        }

        public LanguagePackageProviderKind Kind => LanguagePackageProviderKind.InMemory;

        public List<string> LoadedCultures { get; } = [];

        public ValueTask<LanguagePackageLoadResult> LoadAsync(
            LanguagePackageDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            LoadedCultures.Add(descriptor.Culture.Name);

            return _packages.TryGetValue(GetKey(descriptor), out var package)
                ? ValueTask.FromResult(LanguagePackageLoadResult.Success(package))
                : ValueTask.FromResult(
                    LanguagePackageLoadResult.Failed(
                        new LocalizationError(
                            LocalizationErrorKind.PackageNotFound,
                            "Package was not found.")));
        }

        private static string GetKey(LanguagePackageDescriptor descriptor)
        {
            return $"{descriptor.Culture.Name}|{descriptor.PackageId}";
        }
    }
}
