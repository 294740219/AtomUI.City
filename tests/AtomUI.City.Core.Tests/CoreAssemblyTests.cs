using System.Reflection;
using AtomUI.City.Hosting;

namespace AtomUI.City.Core.Tests;

public sealed class CoreAssemblyTests
{
    [Fact]
    public void CoreAssemblyCanBeLoaded()
    {
        var assembly = Assembly.Load("AtomUI.City.Core");

        Assert.Equal("AtomUI.City.Core", assembly.GetName().Name);
    }

    [Fact]
    public void CoreAssemblyDoesNotReferenceUiBuildOrTestingAssemblies()
    {
        var referenced = typeof(ApplicationHost).Assembly
            .GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToArray();

        Assert.DoesNotContain("Avalonia", referenced);
        Assert.DoesNotContain("AtomUI", referenced);
        Assert.DoesNotContain("Microsoft.CodeAnalysis.CSharp", referenced);
        Assert.DoesNotContain("AtomUI.City.Cli", referenced);
        Assert.DoesNotContain("AtomUI.City.Templates", referenced);
        Assert.DoesNotContain("AtomUI.City.Testing", referenced);
    }
}
