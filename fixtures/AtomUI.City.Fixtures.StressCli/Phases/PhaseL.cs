using System.Globalization;
using System.Security.Cryptography;
using AtomUI.City.Fixtures.StressCli.Infrastructure;
using AtomUI.City.Fixtures.StressCli.Localization;
using AtomUI.City.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Fixtures.StressCli;

/// <summary>真实 Provider、生成清单、文件完整性和运行时撤销。</summary>
public static class PhaseL
{
    private const string FileRouteId = "fixtures.routes.file-provider";
    private const string FileContributionId = "fixtures.file.localization";

    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        FixtureState.Reset();
        await using var host = StressHost.CreateBuilder().Build();
        await host.StartAsync(cancellationToken).ConfigureAwait(false);

        var services = host.Services;
        var localization = services.GetRequiredService<ILocalizationService>();
        var registry = services.GetRequiredService<LanguagePackageRegistry>();
        var providers = services.GetServices<ILanguagePackageProvider>().ToArray();
        var assemblyProvider = providers.Single(provider => provider.Kind == LanguagePackageProviderKind.Assembly);
        var fileProvider = providers.Single(provider => provider.Kind == LanguagePackageProviderKind.File);

        AtomUI.City.Generated.GeneratedLocalizationManifest.RegisterPackages(
            registry,
            "fixtures.generated.owner");
        var generatedManifestOk =
            AtomUI.City.Generated.GeneratedLocalizationManifest.SupportedCultures.SequenceEqual(
                ["en-US", "zh-CN"],
                StringComparer.Ordinal)
            && AtomUI.City.Generated.GeneratedLocalizationManifest.ResourceKeys.Contains(
                AtomUI.City.Generated.GeneratedLocalizationManifest.Keys.Generated_Title,
                StringComparer.Ordinal)
            && registry.Descriptors.Count(descriptor => descriptor.PackageId == "Fixture.Generated") == 2;

        var packageRoot = Path.Combine(AppContext.BaseDirectory, "Localization", "FilePackages");
        var fileDescriptors = new[]
        {
            CreateFileDescriptor(packageRoot, "en-US"),
            CreateFileDescriptor(packageRoot, "zh-CN"),
        };
        var fileRegistration = registry.RegisterRange(fileDescriptors, "fixtures.file.owner");

        var generatedContext = new LocalizationLookupContext(moduleId: GeneratedLocalizationDeclarations.ModuleId);
        var fileContext = new LocalizationLookupContext(routeId: FileRouteId);
        var combinedContext = new LocalizationLookupContext(
            moduleId: GeneratedLocalizationDeclarations.ModuleId,
            routeId: FileRouteId);
        using var generatedLease = localization.ActivateScope(generatedContext);
        using var fileLease = localization.ActivateScope(fileContext);

        var generatedEnglish = await localization.GetStringAsync(
            AtomUI.City.Generated.GeneratedLocalizationManifest.Keys.Generated_Title,
            generatedContext,
            cancellationToken).ConfigureAwait(false);
        var fileEnglish = await localization.GetStringAsync(
            "Provider.Source",
            fileContext,
            cancellationToken).ConfigureAwait(false);
        var priorityEnglish = await localization.GetStringAsync(
            "Provider.Source",
            combinedContext,
            cancellationToken).ConfigureAwait(false);
        var assemblyAndFileOk = fileRegistration.Succeeded
            && generatedEnglish.Value == "Generated assembly operations"
            && fileEnglish.Value == "File provider"
            && priorityEnglish.Value == "File provider";

        var generatedText = await localization.CreateTextAsync(
            "Generated.Title",
            generatedContext,
            cancellationToken).ConfigureAwait(false);
        var generatedChanges = 0;
        generatedText.Changed += (_, _) => Interlocked.Increment(ref generatedChanges);
        var chineseSwitch = await localization.SetCultureAsync("zh-CN", cancellationToken).ConfigureAwait(false);
        var generatedMessage = await localization.GetMessageAsync(
            "Generated.Formatted",
            ["G-2048", 12_345],
            generatedContext,
            cancellationToken).ConfigureAwait(false);
        var fileChinese = await localization.GetStringAsync(
            "Provider.Source",
            fileContext,
            cancellationToken).ConfigureAwait(false);
        var cultureAndFormattingOk = chineseSwitch.Succeeded
            && generatedText.Value == "生成式程序集运营"
            && generatedChanges == 1
            && generatedMessage.Value.Contains("G-2048", StringComparison.Ordinal)
            && generatedMessage.Value.Contains("12,345", StringComparison.Ordinal)
            && fileChinese.Value == "文件语言包";

        var discovered = ((AssemblyLanguagePackageProvider)assemblyProvider)
            .Discover(typeof(PhaseL).Assembly)
            .Where(descriptor => descriptor.PackageId == "Fixture.Generated")
            .ToArray();
        var directAssemblyLoads = await Task.WhenAll(
            discovered.Select(descriptor => assemblyProvider.LoadAsync(descriptor, cancellationToken).AsTask()))
            .ConfigureAwait(false);
        var directProviderOk = discovered.Length == 2
            && directAssemblyLoads.All(result => result.Succeeded)
            && directAssemblyLoads.All(result => result.Package!.TryGetString("Generated.Title", out _));

        var invalidChecksumDescriptor = new LanguagePackageDescriptor(
            "Fixture.File",
            CultureInfo.GetCultureInfo("en-US"),
            ResourceScope.Route)
        {
            ScopeId = FileRouteId,
            ProviderKind = LanguagePackageProviderKind.File,
            Location = Path.Combine(packageRoot, "Fixture.File.en-US.locpack.json"),
            AllowedRootPath = packageRoot,
            Version = "2.0.0",
            Checksum = "sha256:" + new string('0', 64),
        };
        var invalidChecksum = await fileProvider.LoadAsync(invalidChecksumDescriptor, cancellationToken)
            .ConfigureAwait(false);
        var outsideRoot = await fileProvider.LoadAsync(
            new LanguagePackageDescriptor(
                "Fixture.File",
                CultureInfo.GetCultureInfo("en-US"),
                ResourceScope.Route)
            {
                ScopeId = FileRouteId,
                ProviderKind = LanguagePackageProviderKind.File,
                Location = typeof(PhaseL).Assembly.Location,
                AllowedRootPath = packageRoot,
            },
            cancellationToken).ConfigureAwait(false);
        var missingFile = await fileProvider.LoadAsync(
            new LanguagePackageDescriptor(
                "Fixture.File.Missing",
                CultureInfo.GetCultureInfo("en-US"),
                ResourceScope.Route)
            {
                ScopeId = FileRouteId,
                ProviderKind = LanguagePackageProviderKind.File,
                Location = Path.Combine(packageRoot, "missing.locpack.json"),
                AllowedRootPath = packageRoot,
            },
            cancellationToken).ConfigureAwait(false);
        var integrityFailuresOk = invalidChecksum.Error?.Kind == LocalizationErrorKind.PackageChecksumMismatch
            && outsideRoot.Error?.Kind == LocalizationErrorKind.InvalidDescriptor
            && missingFile.Error?.Kind == LocalizationErrorKind.PackageNotFound;

        var fileRevoked = await localization.RevokePackagesByContributionIdAsync(
            FileContributionId,
            cancellationToken).ConfigureAwait(false);
        var fallbackToAssembly = await localization.GetStringAsync(
            "Provider.Source",
            combinedContext,
            cancellationToken).ConfigureAwait(false);
        var generatedRevoked = await localization.RevokePackagesByContributionIdAsync(
            GeneratedLocalizationDeclarations.ContributionId,
            cancellationToken).ConfigureAwait(false);
        var generatedMissing = await localization.GetStringAsync(
            "Generated.Title",
            generatedContext,
            cancellationToken).ConfigureAwait(false);
        var revokeOk = fileRevoked == 2
            && fallbackToAssembly.Value == "程序集语言包"
            && generatedRevoked == 2
            && generatedMissing.IsMissing
            && generatedText.IsMissing
            && generatedChanges == 3
            && registry.Descriptors.All(descriptor =>
                descriptor.ContributionId is not FileContributionId
                    and not GeneratedLocalizationDeclarations.ContributionId);

        generatedText.Dispose();

        FixtureState.Report.Record(
            "L01-localization-generated-manifest",
            "Generator 输出 culture、key 常量和原子注册入口可在真实 Host 使用",
            generatedManifestOk);
        FixtureState.Report.Record(
            "L02-localization-assembly-file",
            "Assembly 与 File Provider 经 LocalizationService 完成真实查找",
            assemblyAndFileOk,
            $"assembly={generatedEnglish.Value} file={fileEnglish.Value}");
        FixtureState.Report.Record(
            "L03-localization-provider-culture",
            "真实 Provider 在 culture 切换后刷新动态文本并按命中文化格式化",
            cultureAndFormattingOk,
            $"changes={generatedChanges} message={generatedMessage.Value}");
        FixtureState.Report.Record(
            "L04-localization-assembly-discovery",
            "Assembly provider 发现并加载生成声明的两个嵌入语言包",
            directProviderOk,
            $"discovered={discovered.Length}");
        FixtureState.Report.Record(
            "L05-localization-file-integrity",
            "File provider 拒绝 checksum、越界路径和不存在文件",
            integrityFailuresOk,
            $"checksum={invalidChecksum.Error?.Kind} outside={outsideRoot.Error?.Kind} missing={missingFile.Error?.Kind}");
        FixtureState.Report.Record(
            "L06-localization-provider-revoke",
            "运行时撤销 File/Assembly contribution 后立即 fallback 且缓存不复活",
            revokeOk,
            $"file={fileRevoked} assembly={generatedRevoked}");

        await host.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private static LanguagePackageDescriptor CreateFileDescriptor(string packageRoot, string cultureName)
    {
        var path = Path.Combine(packageRoot, $"Fixture.File.{cultureName}.locpack.json");
        var checksum = "sha256:" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return new LanguagePackageDescriptor(
            "Fixture.File",
            CultureInfo.GetCultureInfo(cultureName),
            ResourceScope.Route)
        {
            ScopeId = FileRouteId,
            ProviderKind = LanguagePackageProviderKind.File,
            Location = path,
            AllowedRootPath = packageRoot,
            Version = "2.0.0",
            Checksum = checksum,
            ContributionId = FileContributionId,
        };
    }
}
