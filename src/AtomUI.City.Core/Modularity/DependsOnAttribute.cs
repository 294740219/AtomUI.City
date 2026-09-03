namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents depends on attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DependsOnAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the depends on attribute class.
    /// </summary>
    public DependsOnAttribute(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);

        ModuleType = moduleType;
    }

    /// <summary>
    /// Gets the module type value.
    /// </summary>
    public Type ModuleType { get; }

    /// <summary>
    /// Gets or sets the optional value.
    /// </summary>
    public bool Optional { get; set; }
}
