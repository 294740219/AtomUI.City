namespace AtomUI.City.Presentation;

public sealed class ViewLookupRequest
{
    public ViewLookupRequest(
        Type viewModelType,
        string? viewKey = null,
        string? routeId = null,
        string? ownerId = null)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        ViewModelType = viewModelType;
        ViewKey = string.IsNullOrWhiteSpace(viewKey) ? null : viewKey;
        RouteId = string.IsNullOrWhiteSpace(routeId) ? null : routeId;
        OwnerId = string.IsNullOrWhiteSpace(ownerId) ? null : ownerId;
    }

    public Type ViewModelType { get; }

    public string? ViewKey { get; }

    public string? RouteId { get; }

    public string? OwnerId { get; }
}
