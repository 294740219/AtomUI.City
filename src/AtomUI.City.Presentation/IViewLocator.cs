namespace AtomUI.City.Presentation;

public interface IViewLocator
{
    bool TryLocate(
        Type viewModelType,
        string? viewKey,
        out ViewDescriptor? descriptor);

    bool TryLocate(
        ViewLookupRequest request,
        out ViewDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TryLocate(request.ViewModelType, request.ViewKey, out descriptor);
    }

    ViewDescriptor Locate(Type viewModelType, string? viewKey = null);

    ViewDescriptor Locate(ViewLookupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Locate(request.ViewModelType, request.ViewKey);
    }
}
