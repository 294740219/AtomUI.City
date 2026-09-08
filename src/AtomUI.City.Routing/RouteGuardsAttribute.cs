namespace AtomUI.City.Routing;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteGuardsAttribute(params Type[] guardTypes) : Attribute
{
    public IReadOnlyList<Type> GuardTypes { get; } = Array.AsReadOnly(
        (guardTypes ?? throw new ArgumentNullException(nameof(guardTypes))).ToArray());
}
