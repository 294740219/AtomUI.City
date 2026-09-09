using System.Reflection;
using AtomUI.City.Security;

namespace AtomUI.City.Security.Tests;

public sealed class SecurityAssemblyTests
{
    [Fact]
    public void SecurityAssemblyCanBeLoaded()
    {
        var assembly = Assembly.Load("AtomUI.City.Security");

        Assert.Equal("AtomUI.City.Security", assembly.GetName().Name);
    }

    [Fact]
    public void SecurityAssemblyOnlyReferencesDeclaredFrameworkModules()
    {
        var references = typeof(AuthenticationStateStore).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToArray();

        Assert.Contains("AtomUI.City.Core", references);
        Assert.Contains("AtomUI.City.Routing", references);
        Assert.DoesNotContain("AtomUI.City.State", references);
        Assert.DoesNotContain("AtomUI.City.Presentation", references);
        Assert.DoesNotContain("Avalonia", references);
    }
}
