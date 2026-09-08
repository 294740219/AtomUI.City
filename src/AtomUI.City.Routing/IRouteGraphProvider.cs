namespace AtomUI.City.Routing;

public interface IRouteGraphProvider
{
    RouteGraphSnapshot CurrentSnapshot { get; }
}
