namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents module attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ModuleAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the module attribute class.
    /// </summary>
    public ModuleAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the module attribute class.
    /// </summary>
    public ModuleAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    /// <summary>
    /// Gets the name value.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets or sets the version value.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the description value.
    /// </summary>
    public string? Description { get; set; }
}
