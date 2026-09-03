using System.Reflection;
using AtomUI.City.Generators.DependencyInjection;
using AtomUI.City.Generators.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Generators.Tests;

public sealed class AtomUICityIncrementalGeneratorDependencyInjectionTests
{
    [Fact]
    public void GeneratorEmitsModuleOwnedServiceRegistrar()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Core.DependencyInjection;
            using AtomUI.City.Core.Modularity;
            using Microsoft.Extensions.DependencyInjection;

            namespace Sample.App;

            [ServiceRegistrationOwner]
            public sealed class AppModule : ModuleBase;

            public interface IClock { }

            [Service(ServiceLifetime.Singleton)]
            [ExposeServices(typeof(IClock))]
            public sealed class SystemClock : IClock;
            """);

        var result = Run(compilation, out var outputCompilation);
        var generated = Assert.Single(result.GeneratedSources, source =>
            source.HintName.Contains("DependencyInjection", StringComparison.Ordinal));
        var source = generated.SourceText.ToString();

        Assert.Empty(result.Diagnostics);
        Assert.Contains("context.Register(typeof(global::Sample.App.AppModule)", source, StringComparison.Ordinal);
        Assert.Contains("typeof(global::Sample.App.IClock)", source, StringComparison.Ordinal);
        Assert.Contains("typeof(global::Sample.App.SystemClock)", source, StringComparison.Ordinal);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void GeneratorRejectsServicesWithoutOwner()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Core.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;

            [Service(ServiceLifetime.Singleton)]
            public sealed class SystemClock;
            """);

        var result = Run(compilation, out _);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Contains("ServiceRegistrationOwner", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorRejectsDisposableMultiContractService()
    {
        var compilation = CreateCompilation(
            """
            using System;
            using AtomUI.City.Core.DependencyInjection;
            using AtomUI.City.Core.Modularity;

            [ServiceRegistrationOwner]
            public sealed class AppModule : ModuleBase;
            public interface IReader { }
            public interface IWriter { }

            [ScopedService(typeof(IReader), typeof(IWriter))]
            public sealed class Store : IReader, IWriter, IDisposable
            {
                public void Dispose() { }
            }
            """);

        var result = Run(compilation, out _);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("cannot expose multiple", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorRejectsUnknownLifetimeWithoutEmittingRegistrar()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Core.DependencyInjection;
            using AtomUI.City.Core.Modularity;
            using Microsoft.Extensions.DependencyInjection;

            [ServiceRegistrationOwner]
            public sealed class AppModule : ModuleBase;

            [Service((ServiceLifetime)999)]
            public sealed class InvalidService;
            """);

        var result = Run(compilation, out _);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Contains("unknown ServiceLifetime", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(result.GeneratedSources, source =>
            source.HintName.Contains("DependencyInjection", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratorRejectsNullExposedTypeWithoutEmittingRegistrar()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Core.DependencyInjection;
            using AtomUI.City.Core.Modularity;
            using Microsoft.Extensions.DependencyInjection;

            [ServiceRegistrationOwner]
            public sealed class AppModule : ModuleBase;

            [Service(ServiceLifetime.Singleton)]
            [ExposeServices(null)]
            public sealed class InvalidService;
            """);

        var result = Run(compilation, out _);
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Equal(GeneratorDiagnosticIds.InvalidManifestInput, diagnostic.Id);
        Assert.Contains("null or invalid exposed service type", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(result.GeneratedSources, source =>
            source.HintName.Contains("DependencyInjection", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneratorAggregatesServiceRegistrarFromReferencedAssembly()
    {
        var libraryCompilation = CreateCompilation(
            """
            using AtomUI.City.Core.DependencyInjection;
            using AtomUI.City.Core.Modularity;
            using Microsoft.Extensions.DependencyInjection;

            namespace Sample.Library;

            [ServiceRegistrationOwner]
            public sealed class LibraryModule : ModuleBase;

            [Service(ServiceLifetime.Singleton)]
            public sealed class LibraryService;
            """,
            "Sample.Library");
        GeneratorDriver libraryDriver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());
        libraryDriver = libraryDriver.RunGeneratorsAndUpdateCompilation(libraryCompilation, out var generatedLibrary, out _);
        using var image = new MemoryStream();
        var emit = generatedLibrary.Emit(image);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        var applicationCompilation = CreateCompilation(
            """
            using AtomUI.City.Core.Modularity;

            namespace Sample.App;

            [DependsOn(typeof(Sample.Library.LibraryModule))]
            public sealed class AppModule : ModuleBase;
            """,
            "Sample.App",
            [MetadataReference.CreateFromImage(image.ToArray())]);
        var result = Run(applicationCompilation, out var outputCompilation);
        var generated = Assert.Single(result.GeneratedSources, source =>
            source.HintName.Contains("DependencyInjection", StringComparison.Ordinal));

        Assert.Contains(
            $"context.RegisterRegistrar(typeof(global::{ServiceRegistrarSourceBuilder.GetRegistrarTypeName("Sample.Library")}), static () => new global::{ServiceRegistrarSourceBuilder.GetRegistrarTypeName("Sample.Library")}());",
            generated.SourceText.ToString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"new global::{ServiceRegistrarSourceBuilder.GetRegistrarTypeName("Sample.Library")}().Register(context);",
            generated.SourceText.ToString(),
            StringComparison.Ordinal);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void GeneratorProducesStableServiceRegistrarAcrossTwentyDeclarationOrders()
    {
        string? expected = null;
        for (var seed = 0; seed < 20; seed++)
        {
            var random = new Random(seed);
            var declarations = Enumerable.Range(0, 24)
                .OrderBy(_ => random.Next())
                .Select(index => $"[Service(ServiceLifetime.Transient)] public sealed class Service{index:D2};");
            var source = $$"""
                using AtomUI.City.Core.DependencyInjection;
                using AtomUI.City.Core.Modularity;
                using Microsoft.Extensions.DependencyInjection;

                namespace Stress;

                [ServiceRegistrationOwner]
                public sealed class StressModule : ModuleBase;

                {{string.Join(Environment.NewLine, declarations)}}
                """;
            var result = Run(CreateCompilation(source), out var outputCompilation);
            var generated = Assert.Single(result.GeneratedSources, item =>
                item.HintName.Contains("DependencyInjection", StringComparison.Ordinal)).SourceText.ToString();

            expected ??= generated;
            Assert.Equal(expected, generated);
            Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        }
    }

    private static GeneratorRunResult Run(CSharpCompilation compilation, out Compilation outputCompilation)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out _);
        return Assert.Single(driver.GetRunResult().Results);
    }

    private static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "Sample.App",
        IReadOnlyList<MetadataReference>? additionalReferences = null)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Concat([
                MetadataReference.CreateFromFile(typeof(AtomUI.City.Core.Modularity.ModuleBase).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ServiceLifetime).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            ])
            .DistinctBy(reference => reference.Display)
            .Concat(additionalReferences ?? [])
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
