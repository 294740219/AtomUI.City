using System.Globalization;
using AtomUI.City.State;

namespace AtomUI.City.Localization;

public interface ILocalizationService : IDisposable, IAsyncDisposable
{
    CultureInfo CurrentCulture { get; }

    long CultureRevision { get; }

    IReadOnlyState<CultureState> CultureState { get; }

    ILocalizationScopeLease ActivateScope(LocalizationLookupContext context);

    ValueTask<LocalizationResult> SetCultureAsync(
        string cultureName,
        CancellationToken cancellationToken = default);

    ValueTask<LocalizedString> GetStringAsync(
        string key,
        CancellationToken cancellationToken = default);

    ValueTask<LocalizedString> GetStringAsync(
        string key,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default);

    ValueTask<LocalizedMessage> GetMessageAsync(
        string key,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default);

    ValueTask<LocalizedMessage> GetMessageAsync(
        string key,
        IReadOnlyList<object?> arguments,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default);

    ValueTask<ILocalizedText> CreateTextAsync(
        string key,
        CancellationToken cancellationToken = default);

    ValueTask<ILocalizedText> CreateTextAsync(
        string key,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default);

    ValueTask<ILocalizedText> CreateMessageTextAsync(
        string key,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default);

    ValueTask<ILocalizedText> CreateMessageTextAsync(
        string key,
        IReadOnlyList<object?> arguments,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default);

    ValueTask<int> RevokePackagesByContributionIdAsync(
        string contributionId,
        CancellationToken cancellationToken = default);
}
