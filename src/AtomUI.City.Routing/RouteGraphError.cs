namespace AtomUI.City.Routing;

public enum RouteGraphError
{
    DuplicateRouteId,
    DuplicateRouteTemplate,
    MissingParentRoute,
    InvalidRouteTemplate,
    InvalidContribution,
    CircularParentRoute,
    InvalidRouteDefinition,
    DuplicateIndexRoute,
    DuplicateExtensionPoint,
    MissingExtensionPoint,
    MissingRedirectTarget,
    CircularRedirect,
    InvalidVersion,
}
