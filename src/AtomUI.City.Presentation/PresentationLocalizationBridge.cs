using AtomUI.City.Localization;
using AtomUI.City.Core.Threading;

namespace AtomUI.City.Presentation;

public sealed class PresentationLocalizationBridge : IPresentationLocalizationBridge
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IReadOnlyList<IPresentationCultureApplier> _appliers;

    public PresentationLocalizationBridge(
        IUiDispatcher dispatcher,
        IEnumerable<IPresentationCultureApplier> appliers)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(appliers);

        _dispatcher = dispatcher;
        _appliers = appliers.ToArray();
    }

    public async ValueTask<LocalizationResult> ApplyCultureAsync(
        CultureState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var result = LocalizationResult.Success();

        try
        {
            await _dispatcher
                .PostAsync(
                    async dispatcherCancellationToken =>
                    {
                        foreach (var applier in _appliers)
                        {
                            dispatcherCancellationToken.ThrowIfCancellationRequested();

                            try
                            {
                                var applyResult = await applier
                                    .ApplyCultureAsync(state, dispatcherCancellationToken)
                                    .ConfigureAwait(false);

                                if (!applyResult.Succeeded && result.Succeeded)
                                {
                                    result = applyResult;
                                }
                            }
                            catch (OperationCanceledException)
                                when (dispatcherCancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception exception) when (result.Succeeded)
                            {
                                result = LocalizationResult.Failed(
                                    new LocalizationError(
                                        LocalizationErrorKind.PresentationApplyFailed,
                                        "Presentation culture apply failed.",
                                        exception));
                            }
                        }
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            result = LocalizationResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PresentationApplyFailed,
                    "Presentation culture apply failed.",
                    exception));
        }

        return result;
    }
}
