using AtomUI.City.Routing;

namespace AtomUI.City.Security;

public sealed class SecurityRouteGuardOptions
{
    public string? LoginRouteId { get; init; }

    public NavigationOptions LoginNavigationOptions { get; init; } = NavigationOptions.Default;
}
