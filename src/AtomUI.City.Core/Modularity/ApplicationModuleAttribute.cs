namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents application module attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ApplicationModuleAttribute : Attribute
{
}
