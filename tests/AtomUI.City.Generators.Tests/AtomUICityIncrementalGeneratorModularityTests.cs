using System.Reflection;
using AtomUI.City.Core.Modularity;
using AtomUI.City.Generators.Diagnostics;
using AtomUI.City.Generators.Modularity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AtomUI.City.Generators.Tests;

public sealed class AtomUICityIncrementalGeneratorModularityTests
{
    [Fact]
    public void GeneratorEmitsModuleCatalogRegistrarAndApplicationRoot()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Core.Modularity;

            namespace Sample.App;

            public sealed class FoundationModule : ModuleBase;

            [ApplicationModule]
            [DependsOn(typeof(FoundationModule))]
            public sealed class AppModule : ModuleBase;

            public sealed class UnusedModule : ModuleBase;

            public static class Program
            {
                public static void Main()
                {
                }
            }
            """,
            outputKind: OutputKind.ConsoleApplication);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var result = Assert.Single(driver.GetRunResult().Results);
        var generated = Assert.Single(result.GeneratedSources);
        var source = generated.SourceText.ToString();

        Assert.Empty(diagnostics);
        Assert.Equal("AtomUI.City/Modularity/Sample.App.Modules.g.cs", generated.HintName);
        Assert.Contains("static () => new global::Sample.App.FoundationModule()", source, StringComparison.Ordinal);
        Assert.Contains("static () => new global::Sample.App.AppModule()", source, StringComparison.Ordinal);
        Assert.Contains("static () => new global::Sample.App.UnusedModule()", source, StringComparison.Ordinal);
        Assert.Contains("context.AddApplicationRoot(typeof(global::Sample.App.AppModule));", source, StringComparison.Ordinal);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void GeneratorRejectsMultipleApplicationRoots()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Core.Modularity;

            namespace Sample.App;

            [ApplicationModule]
            public sealed class FirstModule : ModuleBase;

            [ApplicationModule]
            public sealed class SecondModule : ModuleBase;
            """,
            outputKind: OutputKind.ConsoleApplication);
        var result = Run(compilation);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(2, result.Diagnostics.Length);
        Assert.All(result.Diagnostics, diagnostic =>
            Assert.Equal(GeneratorDiagnosticIds.MultipleApplicationModules, diagnostic.Id));
    }

    [Fact]
    public void GeneratorRejectsApplicationRootWithoutParameterlessConstructor()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Core.Modularity;

            namespace Sample.App;

            [ApplicationModule]
            public sealed class AppModule : ModuleBase
            {
                public AppModule(string value)
                {
                }
            }
            """,
            outputKind: OutputKind.ConsoleApplication);
        var result = Run(compilation);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(GeneratorDiagnosticIds.InvalidGeneratedModule, diagnostic.Id);
    }

    [Fact]
    public void GeneratorReportsCircularModuleDependency()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Core.Modularity;

            namespace Sample.App;

            [ApplicationModule]
            [DependsOn(typeof(SecondModule))]
            public sealed class FirstModule : ModuleBase;

            [DependsOn(typeof(FirstModule))]
            public sealed class SecondModule : ModuleBase;
            """,
            outputKind: OutputKind.ConsoleApplication);
        var result = Run(compilation);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(GeneratorDiagnosticIds.CircularModuleDependency, diagnostic.Id);
    }

    [Fact]
    public void GeneratorAggregatesRegistrarFromReferencedAssembly()
    {
        var libraryCompilation = CreateCompilation(
            """
            using AtomUI.City.Core.Modularity;

            namespace Sample.Library;

            public sealed class FoundationModule : ModuleBase;
            """,
            "Sample.Library");
        GeneratorDriver libraryDriver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        libraryDriver = libraryDriver.RunGeneratorsAndUpdateCompilation(
            libraryCompilation,
            out var generatedLibraryCompilation,
            out var libraryDiagnostics);

        Assert.Empty(libraryDiagnostics);
        using var libraryImage = new MemoryStream();
        var emitResult = generatedLibraryCompilation.Emit(libraryImage);
        Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));

        var libraryReference = MetadataReference.CreateFromImage(libraryImage.ToArray());
        var applicationCompilation = CreateCompilation(
            """
            using AtomUI.City.Core.Modularity;

            namespace Sample.App;

            [ApplicationModule]
            [DependsOn(typeof(Sample.Library.FoundationModule))]
            public sealed class AppModule : ModuleBase;

            public static class Program
            {
                public static void Main()
                {
                }
            }
            """,
            "Sample.App",
            [libraryReference],
            OutputKind.ConsoleApplication);
        GeneratorDriver applicationDriver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        applicationDriver = applicationDriver.RunGeneratorsAndUpdateCompilation(
            applicationCompilation,
            out var generatedApplicationCompilation,
            out var applicationDiagnostics);

        var result = Assert.Single(applicationDriver.GetRunResult().Results);
        var source = Assert.Single(result.GeneratedSources).SourceText.ToString();
        var libraryRegistrar = ModuleRegistrarSourceBuilder.GetRegistrarTypeName("Sample.Library");

        Assert.Empty(applicationDiagnostics);
        Assert.Contains($"new global::{libraryRegistrar}().Register(context);", source, StringComparison.Ordinal);
        Assert.Contains("context.AddApplicationRoot(typeof(global::Sample.App.AppModule));", source, StringComparison.Ordinal);
        Assert.Empty(generatedApplicationCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void GeneratorRejectsApplicationModuleInLibraryProject()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Core.Modularity;

            namespace Sample.Library;

            [ApplicationModule]
            public sealed class LibraryModule : ModuleBase;
            """,
            "Sample.Library");
        var result = Run(compilation);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(GeneratorDiagnosticIds.InvalidGeneratedModule, diagnostic.Id);
        Assert.Contains("executable application project", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorRejectsApplicationModuleAttributeOnNonModuleType()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Core.Modularity;

            namespace Sample.App;

            [ApplicationModule]
            public sealed class NotAModule;
            """,
            outputKind: OutputKind.ConsoleApplication);
        var result = Run(compilation);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Empty(result.GeneratedSources);
        Assert.Equal(GeneratorDiagnosticIds.InvalidGeneratedModule, diagnostic.Id);
        Assert.Contains("does not implement IModule", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    private static GeneratorRunResult Run(CSharpCompilation compilation)
    {
        var driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());
        return Assert.Single(driver.RunGenerators(compilation).GetRunResult().Results);
    }

    private static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "Sample.App",
        IReadOnlyList<MetadataReference>? additionalReferences = null,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary)
    {
        var sourceTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(ModuleBase).Assembly.Location))
            .DistinctBy(reference => reference.Display)
            .Concat(additionalReferences ?? [])
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName,
            [sourceTree],
            references,
            new CSharpCompilationOptions(outputKind));
    }
}
