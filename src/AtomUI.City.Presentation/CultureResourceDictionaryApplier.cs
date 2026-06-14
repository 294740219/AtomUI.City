using AtomUI.City.Diagnostics;
using AtomUI.City.Localization;

namespace AtomUI.City.Presentation;

public sealed class CultureResourceDictionaryApplier : IPresentationCultureApplier
{
    private readonly IReadOnlyList<IPresentationResourceDictionaryTarget> _targets;
    private readonly IHostDiagnostics? _diagnostics;

    public CultureResourceDictionaryApplier(IEnumerable<IPresentationResourceDictionaryTarget> targets)
        : this(targets, diagnostics: null)
    {
    }

    public CultureResourceDictionaryApplier(
        IEnumerable<IPresentationResourceDictionaryTarget> targets,
        IHostDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(targets);

        _targets = targets.ToArray();
        _diagnostics = diagnostics;
    }

    public async ValueTask<LocalizationResult> ApplyCultureAsync(
        CultureState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var result = LocalizationResult.Success();

        foreach (var target in _targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var targetResult = await target
                    .ApplyResourcesAsync(state, cancellationToken)
                    .ConfigureAwait(false);

                if (!targetResult.Succeeded && result.Succeeded)
                {
                    result = targetResult;
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (result.Succeeded)
            {
                result = LocalizationResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.PresentationApplyFailed,
                        "Presentation resource dictionary apply failed.",
                        exception));
            }
        }

        if (result.Succeeded)
        {
            WriteAppliedDiagnostic(state);
        }
        else
        {
            WriteApplyFailedDiagnostic(state, result);
        }

        return result;
    }

    private void WriteAppliedDiagnostic(CultureState state)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ResourceDictionaryApplied,
            $"Presentation resource dictionary applied culture '{state.CurrentCulture.Name}' with packages '{FormatPackageIds(state.LoadedPackageIds)}'.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext(state, result: null),
        });
    }

    private void WriteApplyFailedDiagnostic(
        CultureState state,
        LocalizationResult result)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ResourceDictionaryApplyFailed,
            $"Presentation resource dictionary failed to apply culture '{state.CurrentCulture.Name}' with packages '{FormatPackageIds(state.LoadedPackageIds)}': {result.Error?.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = CreateDiagnosticContext(state, result),
        });
    }

    private IReadOnlyDictionary<string, string?> CreateDiagnosticContext(
        CultureState state,
        LocalizationResult? result)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["culture"] = state.CurrentCulture.Name,
            ["uiCulture"] = state.CurrentUICulture.Name,
            ["packageIds"] = FormatPackageIds(state.LoadedPackageIds),
            ["targetCount"] = _targets.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["errorKind"] = result?.Error?.Kind.ToString(),
            ["error"] = result?.Error?.Exception?.GetType().FullName,
        };
    }

    private static string FormatPackageIds(IReadOnlyList<string> packageIds)
    {
        return packageIds.Count == 0 ? "<none>" : string.Join(", ", packageIds);
    }
}
