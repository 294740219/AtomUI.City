namespace AtomUI.City.Routing;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class QueryAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = string.IsNullOrWhiteSpace(name) ? null : name;
}
