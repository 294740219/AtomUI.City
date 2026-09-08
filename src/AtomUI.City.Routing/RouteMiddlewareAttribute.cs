namespace AtomUI.City.Routing;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteMiddlewareAttribute(params Type[] middlewareTypes) : Attribute
{
    public IReadOnlyList<Type> MiddlewareTypes { get; } = Array.AsReadOnly(
        (middlewareTypes ?? throw new ArgumentNullException(nameof(middlewareTypes))).ToArray());
}
