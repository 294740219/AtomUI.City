using AtomUI.City.Localization;
using AtomUI.City.Mvvm;
using AtomUI.City.Routing;
using AtomUI.City.Core.Threading;

namespace AtomUI.City.Presentation;

public sealed class LocalizedRouteTextBinding
{
    private readonly LocalizedTextBindingSet _bindingSet;

    public LocalizedRouteTextBinding(
        ILocalizationService localization,
        IUiDispatcher dispatcher)
    {
        _bindingSet = new LocalizedTextBindingSet(localization, dispatcher);
    }

    public async ValueTask<IDisposable> BindAsync(
        RouteDescriptor route,
        ILocalizedRouteTextTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(target);

        var resources = new List<IDisposable>();
        var metadata = route.Metadata;
        var context = new LocalizationLookupContext(routeId: route.RouteId);

        try
        {
            resources.Add(_bindingSet.ActivateScope(context));
            await _bindingSet.BindKeyAsync(
                    metadata.TitleKey,
                    value => target.Title = value,
                    resources,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
            await _bindingSet.BindKeyAsync(
                    metadata.DescriptionKey,
                    value => target.Description = value,
                    resources,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
            await _bindingSet.BindKeyAsync(
                    metadata.BreadcrumbKey,
                    value => target.Breadcrumb = value,
                    resources,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
            await _bindingSet.BindKeyAsync(
                    metadata.GroupKey,
                    value => target.Group = value,
                    resources,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
            await _bindingSet.BindKeyAsync(
                    metadata.ErrorTitleKey,
                    value => target.ErrorTitle = value,
                    resources,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);

            return LocalizedTextBindingSet.CreateHandle(resources);
        }
        catch
        {
            LocalizedTextBindingSet.DisposeAll(resources);
            throw;
        }
    }

    public async ValueTask<IDisposable> BindAsync(
        RouteDescriptor route,
        ILocalizedRouteTextTarget target,
        IActivationScope activationScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activationScope);

        var handle = await BindAsync(route, target, cancellationToken).ConfigureAwait(false);
        activationScope.Add(handle);

        return handle;
    }
}
