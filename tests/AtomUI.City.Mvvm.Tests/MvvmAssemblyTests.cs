using System.Reflection;

namespace AtomUI.City.Mvvm.Tests;

public sealed class MvvmAssemblyTests
{
    [Fact]
    public void MvvmAssemblyCanBeLoaded()
    {
        var assembly = Assembly.Load("AtomUI.City.Mvvm");

        Assert.Equal("AtomUI.City.Mvvm", assembly.GetName().Name);
    }

    [Fact]
    public void MvvmAssemblyDoesNotReferencePresentationOrAvaloniaVisuals()
    {
        var assembly = Assembly.Load("AtomUI.City.Mvvm");
        var references = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("AtomUI.City.Presentation", references);
        Assert.DoesNotContain(references, name => name!.StartsWith("Avalonia", StringComparison.Ordinal));
    }
}
