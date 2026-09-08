namespace AtomUI.City.Routing;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteMatchPoliciesAttribute(params Type[] policyTypes) : Attribute
{
    public IReadOnlyList<Type> PolicyTypes { get; } = Array.AsReadOnly(
        (policyTypes ?? throw new ArgumentNullException(nameof(policyTypes))).ToArray());
}
