namespace AtomUI.City.Core.DependencyInjection;

/// <summary>
/// Represents service registration owner attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ServiceRegistrationOwnerAttribute : Attribute
{
}
