using System.Reflection;

namespace AtomUI.City.Routing.Tests;

public sealed class RoutingAssemblyTests
{
    [Fact]
    public void RoutingAssemblyCanBeLoaded()
    {
        var assembly = Assembly.Load("AtomUI.City.Routing");

        Assert.Equal("AtomUI.City.Routing", assembly.GetName().Name);
    }

    [Fact]
    public void RoutingAssemblyDoesNotReferencePresentation()
    {
        var assembly = Assembly.Load("AtomUI.City.Routing");

        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, "AtomUI.City.Presentation", StringComparison.Ordinal));
    }
}
