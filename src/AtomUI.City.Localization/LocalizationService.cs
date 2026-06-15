using System.Globalization;

namespace AtomUI.City.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private readonly IReadOnlyList<LanguagePackageDescriptor> _descriptors;
    private readonly IReadOnlyDictionary<LanguagePackageProviderKind, ILanguagePackageProvider> _providers;
    private readonly IReadOnlyList<CultureInfo> _configuredFallbackCultures;
    private readonly IPresentationLocalizationBridge _bridge;
    private readonly ILocalizationDiagnostics? _diagnostics;
    private readonly Dictionary<(string CultureName, string PackageId), LanguagePackage> _loadedPackages = [];
    private readonly Dictionary<(string CultureName, string PackageId), Task<LanguagePackageLoadResult>> _packageLoadTasks = [];
    private readonly List<ILocalizedText> _localizedTexts = [];
    private readonly object _packageLoadGate = new();
    private readonly object _localizedTextGate = new();
    private readonly SemaphoreSlim _switchLock = new(1, 1);

    public LocalizationService(
        IReadOnlyList<LanguagePackageDescriptor> descriptors,
        IEnumerable<ILanguagePackageProvider> providers,
        IPresentationLocalizationBridge? bridge = null,
        ILocalizationDiagnostics? diagnostics = null)
        : this(
            descriptors,
            providers,
            bridge,
            diagnostics,
            CultureInfo.InvariantCulture,
            CultureInfo.InvariantCulture,
            [])
    {
    }

    public LocalizationService(
        LocalizationOptions options,
        IEnumerable<ILanguagePackageProvider> providers,
        IPresentationLocalizationBridge? bridge = null,
        ILocalizationDiagnostics? diagnostics = null)
        : this(
            GetLanguagePackageDescriptors(options),
            providers,
            bridge,
            diagnostics,
            GetDefaultCulture(options),
            GetDefaultUICulture(options),
            GetConfiguredFallbackCultures(options))
    {
    }

    private LocalizationService(
        IReadOnlyList<LanguagePackageDescriptor> descriptors,
        IEnumerable<ILanguagePackageProvider> providers,
        IPresentationLocalizationBridge? bridge,
        ILocalizationDiagnostics? diagnostics,
        CultureInfo defaultCulture,
        CultureInfo defaultUICulture,
        IReadOnlyList<CultureInfo> configuredFallbackCultures)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(defaultCulture);
        ArgumentNullException.ThrowIfNull(defaultUICulture);
        ArgumentNullException.ThrowIfNull(configuredFallbackCultures);

        _descriptors = descriptors.ToArray();
        _providers = providers.ToDictionary(provider => provider.Kind);
        _configuredFallbackCultures = configuredFallbackCultures.ToArray();
        _bridge = bridge ?? NoopPresentationLocalizationBridge.Instance;
        _diagnostics = diagnostics;
        var fallbackCultures = CreateFallbackCultures(defaultCulture, out var rejectedFallbackCulture);
        if (rejectedFallbackCulture is not null)
        {
            throw new ArgumentException(
                $"Default culture '{defaultCulture.Name}' cannot use itself as a fallback culture.",
                nameof(configuredFallbackCultures));
        }

        State = new CultureState(
            defaultCulture,
            defaultUICulture,
            fallbackCultures,
            revision: 0,
            loadedPackageIds: []);
    }

    public CultureState State { get; private set; }

    public CultureInfo CurrentCulture => State.CurrentCulture;

    public long CultureRevision => State.Revision;

    public async ValueTask<LocalizationResult> SetCultureAsync(
        string cultureName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        await _switchLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!TryGetCulture(cultureName, out var culture, out var invalidCultureError))
            {
                WriteCultureSwitchRejected(cultureName, fallbackCultureName: null, invalidCultureError!);

                return LocalizationResult.Failed(invalidCultureError!);
            }

            var fallbackCultures = CreateFallbackCultures(culture, out var rejectedFallbackCulture);
            if (rejectedFallbackCulture is not null)
            {
                var fallbackCycleError = new LocalizationError(
                    LocalizationErrorKind.InvalidCulture,
                    $"Culture '{culture.Name}' cannot use itself as a fallback culture.");
                WriteCultureSwitchRejected(culture.Name, rejectedFallbackCulture.Name, fallbackCycleError);

                return LocalizationResult.Failed(fallbackCycleError);
            }

            if (string.Equals(culture.Name, CurrentCulture.Name, StringComparison.OrdinalIgnoreCase))
            {
                WriteDiagnostic(
                    LocalizationDiagnosticIds.CultureSwitchSkipped,
                    $"Culture '{culture.Name}' is already active.",
                    LocalizationDiagnosticSeverity.Trace,
                    cultureName: culture.Name,
                    fallbackCultureName: FormatFallbackCultures(State.FallbackCultures));

                return LocalizationResult.Success();
            }

            var targetDescriptors = GetDescriptors(culture).ToArray();
            var pendingPackages = new List<LanguagePackage>();

            foreach (var descriptor in targetDescriptors)
            {
                var loadResult = await LoadPackageAsync(
                        descriptor,
                        cache: false,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!loadResult.Succeeded)
                {
                    DisposeAll(pendingPackages);
                    WritePackageLoadFailed(descriptor, loadResult.Error);

                    return LocalizationResult.Failed(loadResult.Error!);
                }

                pendingPackages.Add(loadResult.Package!);
            }

            var nextState = new CultureState(
                culture,
                culture,
                fallbackCultures,
                State.Revision + 1,
                targetDescriptors.Select(descriptor => descriptor.PackageId).ToArray());

            foreach (var package in pendingPackages)
            {
                lock (_packageLoadGate)
                {
                    _loadedPackages[CreatePackageCacheKey(package.Descriptor)] = package;
                }
            }

            State = nextState;
            WriteDiagnostic(
                LocalizationDiagnosticIds.CultureChanged,
                $"Culture changed to '{culture.Name}'.",
                LocalizationDiagnosticSeverity.Info,
                cultureName: culture.Name,
                fallbackCultureName: FormatFallbackCultures(nextState.FallbackCultures));

            var bridgeResult = await _bridge.ApplyCultureAsync(nextState, cancellationToken).ConfigureAwait(false);
            if (!bridgeResult.Succeeded)
            {
                WriteDiagnostic(
                    LocalizationDiagnosticIds.AtomUiApplyFailed,
                    bridgeResult.Error!.Message,
                    LocalizationDiagnosticSeverity.Error,
                    cultureName: culture.Name,
                    errorKind: bridgeResult.Error.Kind);
            }

            await RefreshLocalizedTextsAsync(cancellationToken).ConfigureAwait(false);

            return bridgeResult.Succeeded ? LocalizationResult.Success() : bridgeResult;
        }
        catch (OperationCanceledException)
        {
            return LocalizationResult.Failed(
                new LocalizationError(LocalizationErrorKind.Cancelled, "Culture switch was cancelled."));
        }
        finally
        {
            _switchLock.Release();
        }
    }

    public async ValueTask<LocalizedString> GetStringAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        foreach (var descriptor in GetDescriptors(CurrentCulture))
        {
            var loadResult = await LoadPackageAsync(descriptor, cache: true, cancellationToken).ConfigureAwait(false);
            if (loadResult.Succeeded && loadResult.Package!.TryGetString(key, out var value))
            {
                return LocalizedString.Found(key, value, descriptor.Culture);
            }

            if (!loadResult.Succeeded)
            {
                WritePackageLoadFailed(descriptor, loadResult.Error);
            }
        }

        foreach (var fallbackCulture in State.FallbackCultures)
        {
            foreach (var descriptor in GetDescriptors(fallbackCulture))
            {
                var loadResult = await LoadPackageAsync(descriptor, cache: true, cancellationToken).ConfigureAwait(false);
                if (loadResult.Succeeded && loadResult.Package!.TryGetString(key, out var value))
                {
                    return LocalizedString.Fallback(key, value, descriptor.Culture);
                }

                if (!loadResult.Succeeded)
                {
                    WritePackageLoadFailed(descriptor, loadResult.Error);
                }
            }
        }

        WriteDiagnostic(
            LocalizationDiagnosticIds.ResourceMissing,
            $"Localized resource '{key}' was not found.",
            LocalizationDiagnosticSeverity.Warning,
            cultureName: CurrentCulture.Name,
            resourceKey: key,
            errorKind: LocalizationErrorKind.ResourceMissing);

        return LocalizedString.Missing(key, CurrentCulture);
    }

    public async ValueTask<LocalizedMessage> GetMessageAsync(
        string key,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(arguments);

        var template = await GetStringAsync(key, cancellationToken).ConfigureAwait(false);

        if (template.IsMissing || arguments.Count == 0)
        {
            return LocalizedMessage.FromString(template, template.Value);
        }

        try
        {
            return LocalizedMessage.FromString(
                template,
                string.Format(template.Culture, template.Value, arguments.ToArray()));
        }
        catch (FormatException exception)
        {
            WriteDiagnostic(
                LocalizationDiagnosticIds.MessageFormatFailed,
                exception.Message,
                LocalizationDiagnosticSeverity.Error,
                cultureName: template.Culture.Name,
                resourceKey: key,
                errorKind: LocalizationErrorKind.FormatFailed);

            return LocalizedMessage.FromString(template, template.Value, isFormatFailed: true);
        }
    }

    public async ValueTask<ILocalizedText> CreateTextAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var text = await LocalizedText.CreateAsync(this, key, cancellationToken).ConfigureAwait(false);
        RegisterLocalizedText(text);

        return text;
    }

    public async ValueTask<ILocalizedText> CreateMessageTextAsync(
        string key,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var text = await LocalizedMessageText.CreateAsync(this, key, arguments, cancellationToken).ConfigureAwait(false);
        RegisterLocalizedText(text);

        return text;
    }

    internal void UnregisterLocalizedText(ILocalizedText text)
    {
        lock (_localizedTextGate)
        {
            _localizedTexts.Remove(text);
        }
    }

    internal void WriteTextRefreshFailed(string key, Exception exception)
    {
        WriteDiagnostic(
            LocalizationDiagnosticIds.TextRefreshFailed,
            exception.Message,
            LocalizationDiagnosticSeverity.Error,
            cultureName: CurrentCulture.Name,
            resourceKey: key,
            errorKind: LocalizationErrorKind.RefreshFailed);
    }

    private void RegisterLocalizedText(ILocalizedText text)
    {
        lock (_localizedTextGate)
        {
            _localizedTexts.Add(text);
        }
    }

    private async ValueTask RefreshLocalizedTextsAsync(CancellationToken cancellationToken)
    {
        ILocalizedText[] localizedTexts;

        lock (_localizedTextGate)
        {
            localizedTexts = _localizedTexts.ToArray();
        }

        foreach (var localizedText in localizedTexts)
        {
            try
            {
                await localizedText.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                WriteTextRefreshFailed(localizedText.Key, exception);
            }
        }
    }

    private async ValueTask<LanguagePackageLoadResult> LoadPackageAsync(
        LanguagePackageDescriptor descriptor,
        bool cache,
        CancellationToken cancellationToken)
    {
        if (!cache)
        {
            return await LoadPackageCoreAsync(descriptor, cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = CreatePackageCacheKey(descriptor);
        Task<LanguagePackageLoadResult> loadTask;
        lock (_packageLoadGate)
        {
            if (_loadedPackages.TryGetValue(cacheKey, out var loadedPackage))
            {
                return LanguagePackageLoadResult.Success(loadedPackage);
            }

            if (!_packageLoadTasks.TryGetValue(cacheKey, out loadTask!))
            {
                loadTask = LoadPackageCoreAsync(descriptor, cancellationToken).AsTask();
                _packageLoadTasks[cacheKey] = loadTask;
            }
        }

        var loadResult = await loadTask.ConfigureAwait(false);

        lock (_packageLoadGate)
        {
            if (_packageLoadTasks.TryGetValue(cacheKey, out var registeredTask)
                && ReferenceEquals(registeredTask, loadTask))
            {
                _packageLoadTasks.Remove(cacheKey);
            }

            if (loadResult.Succeeded)
            {
                _loadedPackages[cacheKey] = loadResult.Package!;
            }
        }

        return loadResult;
    }

    private async ValueTask<LanguagePackageLoadResult> LoadPackageCoreAsync(
        LanguagePackageDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        if (!_providers.TryGetValue(descriptor.ProviderKind, out var provider))
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PackageNotFound,
                    $"No language package provider is registered for '{descriptor.ProviderKind}'."));
        }

        return await provider.LoadAsync(descriptor, cancellationToken).ConfigureAwait(false);
    }

    private static (string CultureName, string PackageId) CreatePackageCacheKey(
        LanguagePackageDescriptor descriptor)
    {
        return (descriptor.Culture.Name, descriptor.PackageId);
    }

    private IEnumerable<LanguagePackageDescriptor> GetDescriptors(CultureInfo culture)
    {
        return _descriptors.Where(descriptor =>
                string.Equals(descriptor.Culture.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(descriptor => GetScopeLookupRank(descriptor.Scope));
    }

    private static int GetScopeLookupRank(ResourceScope scope)
    {
        return scope switch
        {
            ResourceScope.Route => 0,
            ResourceScope.Window => 1,
            ResourceScope.Plugin => 2,
            ResourceScope.Module => 3,
            ResourceScope.Host => 4,
            ResourceScope.Presentation => 5,
            _ => int.MaxValue,
        };
    }

    private IReadOnlyList<CultureInfo> CreateFallbackCultures(
        CultureInfo culture,
        out CultureInfo? rejectedFallbackCulture)
    {
        rejectedFallbackCulture = null;
        var fallbackCultures = new List<CultureInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            culture.Name,
        };

        foreach (var fallbackCulture in GetExplicitFallbackCultures(culture))
        {
            if (string.Equals(fallbackCulture.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
            {
                rejectedFallbackCulture = fallbackCulture;

                return fallbackCultures;
            }

            if (seen.Add(fallbackCulture.Name))
            {
                fallbackCultures.Add(fallbackCulture);
            }
        }

        var parent = culture.Parent;
        while (!string.IsNullOrEmpty(parent.Name))
        {
            if (seen.Add(parent.Name))
            {
                fallbackCultures.Add(parent);
            }

            parent = parent.Parent;
        }

        if (seen.Add(CultureInfo.InvariantCulture.Name))
        {
            fallbackCultures.Add(CultureInfo.InvariantCulture);
        }

        return fallbackCultures;
    }

    private IEnumerable<CultureInfo> GetExplicitFallbackCultures(CultureInfo culture)
    {
        foreach (var fallbackCulture in GetDescriptors(culture)
                     .Select(descriptor => descriptor.FallbackCulture)
                     .Where(fallbackCulture => fallbackCulture is not null)
                     .Cast<CultureInfo>())
        {
            yield return fallbackCulture;
        }

        foreach (var fallbackCulture in _configuredFallbackCultures)
        {
            yield return fallbackCulture;
        }
    }

    private static bool TryGetCulture(
        string cultureName,
        out CultureInfo culture,
        out LocalizationError? error)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
            error = null;

            return true;
        }
        catch (CultureNotFoundException exception)
        {
            culture = CultureInfo.InvariantCulture;
            error = new LocalizationError(
                LocalizationErrorKind.InvalidCulture,
                $"Culture '{cultureName}' is not supported.",
                exception);

            return false;
        }
    }

    private static string FormatFallbackCultures(IEnumerable<CultureInfo> fallbackCultures)
    {
        return string.Join(";", fallbackCultures.Select(culture => culture.Name));
    }

    private static IReadOnlyList<LanguagePackageDescriptor> GetLanguagePackageDescriptors(LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.LanguagePackages.ToArray();
    }

    private static CultureInfo GetDefaultCulture(LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.DefaultCulture;
    }

    private static CultureInfo GetDefaultUICulture(LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.DefaultUICulture;
    }

    private static IReadOnlyList<CultureInfo> GetConfiguredFallbackCultures(LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.FallbackCultures.ToArray();
    }

    private void WritePackageLoadFailed(
        LanguagePackageDescriptor descriptor,
        LocalizationError? error)
    {
        WriteDiagnostic(
            LocalizationDiagnosticIds.PackageLoadFailed,
            error?.Message ?? $"Language package '{descriptor.PackageId}' failed to load.",
            LocalizationDiagnosticSeverity.Error,
            cultureName: descriptor.Culture.Name,
            packageId: descriptor.PackageId,
            scope: descriptor.Scope,
            errorKind: error?.Kind);
    }

    private void WriteDiagnostic(
        string code,
        string message,
        LocalizationDiagnosticSeverity severity,
        string? cultureName = null,
        string? fallbackCultureName = null,
        string? resourceKey = null,
        string? packageId = null,
        ResourceScope? scope = null,
        LocalizationErrorKind? errorKind = null)
    {
        _diagnostics?.Write(
            new LocalizationDiagnosticRecord(
                code,
                message,
                severity,
                CultureName: cultureName,
                FallbackCultureName: fallbackCultureName,
                ResourceKey: resourceKey,
                PackageId: packageId,
                Scope: scope,
                CultureRevision: State.Revision,
                ErrorKind: errorKind));
    }

    private void WriteCultureSwitchRejected(
        string cultureName,
        string? fallbackCultureName,
        LocalizationError error)
    {
        WriteDiagnostic(
            LocalizationDiagnosticIds.CultureSwitchRejected,
            error.Message,
            LocalizationDiagnosticSeverity.Warning,
            cultureName: cultureName,
            fallbackCultureName: fallbackCultureName,
            errorKind: error.Kind);
    }

    private static void DisposeAll(IEnumerable<LanguagePackage> packages)
    {
        foreach (var package in packages)
        {
            package.Dispose();
        }
    }

    private sealed class NoopPresentationLocalizationBridge : IPresentationLocalizationBridge
    {
        public static readonly NoopPresentationLocalizationBridge Instance = new();

        private NoopPresentationLocalizationBridge()
        {
        }

        public ValueTask<LocalizationResult> ApplyCultureAsync(
            CultureState state,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(LocalizationResult.Success());
        }
    }
}
