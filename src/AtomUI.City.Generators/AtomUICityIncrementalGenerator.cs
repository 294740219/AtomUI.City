using System.Text;
using AtomUI.City.Generators.Common;
using AtomUI.City.Generators.Diagnostics;
using AtomUI.City.Generators.Modularity;
using AtomUI.City.Generators.Presentation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using CityModuleMetadata = AtomUI.City.Generators.Modularity.ModuleMetadata;

namespace AtomUI.City.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class AtomUICityIncrementalGenerator : IIncrementalGenerator
{
    private const string ApplicationModuleAttributeName =
        "AtomUI.City.Core.Modularity.ApplicationModuleAttribute";
    private const string GeneratedModuleManifestAttributeName =
        "AtomUI.City.Core.Modularity.GeneratedModuleManifestAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        InitializeModularity(context);
        InitializePresentation(context);
    }

    private static void InitializeModularity(IncrementalGeneratorInitializationContext context)
    {
        var moduleCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, _) => ReadModuleCandidate(syntaxContext))
            .Where(static candidate => candidate is not null)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(moduleCandidates),
            static (sourceContext, value) =>
            {
                var compilation = value.Left;
                var candidates = value.Right
                    .Where(candidate => candidate is not null)
                    .Select(candidate => candidate!)
                    .GroupBy(candidate => candidate.TypeName, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(candidate => candidate.TypeName, StringComparer.Ordinal)
                    .ToArray();
                var validCandidates = new List<ModuleGenerationCandidate>(candidates.Length);

                foreach (var candidate in candidates)
                {
                    if (candidate.Metadata is null)
                    {
                        ReportInvalidGeneratedModule(
                            sourceContext,
                            candidate,
                            $"Type '{candidate.TypeName}' is marked with ApplicationModule but does not implement IModule.");
                        continue;
                    }

                    if (candidate.IsApplicationRoot &&
                        !IsExecutableOutput(compilation.Options.OutputKind))
                    {
                        ReportInvalidGeneratedModule(
                            sourceContext,
                            candidate,
                            $"Application module '{candidate.TypeName}' must be declared by an executable application project.");
                        continue;
                    }

                    if (CanGenerateFactory(candidate.Symbol))
                    {
                        validCandidates.Add(candidate);
                        continue;
                    }

                    if (candidate.IsApplicationRoot)
                    {
                        ReportInvalidGeneratedModule(
                            sourceContext,
                            candidate,
                            $"Module '{candidate.TypeName}' cannot be emitted with a strong-typed factory.");
                    }
                }

                var applicationRoots = validCandidates
                    .Where(candidate => candidate.IsApplicationRoot)
                    .ToArray();

                if (applicationRoots.Length > 1)
                {
                    foreach (var root in applicationRoots)
                    {
                        sourceContext.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
                            GeneratorFeature.Modularity,
                            new GeneratorDiagnostic(
                                GeneratorDiagnostics.MultipleApplicationModules,
                                "An application compilation can declare only one ApplicationModule.",
                                root.TypeName),
                            root.Location));
                    }

                    return;
                }

                var modules = validCandidates
                    .Select(candidate => candidate.Metadata!)
                    .ToArray();
                var availableExternalDependencies = GetAvailableExternalDependencyTypeNames(
                    compilation,
                    modules);
                var graph = ModuleDependencyGraphBuilder.Build(
                    modules,
                    availableExternalDependencies);

                if (graph.Diagnostics.Count > 0)
                {
                    foreach (var diagnostic in graph.Diagnostics)
                    {
                        var location = validCandidates
                            .FirstOrDefault(candidate => string.Equals(
                                candidate.TypeName,
                                diagnostic.Target,
                                StringComparison.Ordinal))
                            ?.Location;
                        sourceContext.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
                            GeneratorFeature.Modularity,
                            diagnostic,
                            location));
                    }

                    return;
                }

                var referencedRegistrars = ReadReferencedRegistrarTypeNames(compilation);

                if (graph.OrderedModules.Count == 0 && referencedRegistrars.Count == 0)
                {
                    return;
                }

                var assemblyName = string.IsNullOrWhiteSpace(compilation.AssemblyName)
                    ? "Assembly"
                    : compilation.AssemblyName!;
                var source = ModuleRegistrarSourceBuilder.Build(
                    assemblyName,
                    graph.OrderedModules,
                    referencedRegistrars);

                sourceContext.AddSource(
                    GeneratedCodeNames.CreateHintName(
                        GeneratorFeature.Modularity,
                        assemblyName,
                        "Modules"),
                    SourceText.From(source, Encoding.UTF8));
            });
    }

    private static void InitializePresentation(IncrementalGeneratorInitializationContext context)
    {
        var presentationViews = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax declaration && declaration.AttributeLists.Count > 0,
                static (syntaxContext, _) => ReadPresentationViews(syntaxContext))
            .Where(static views => views.Count > 0)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(presentationViews),
            static (sourceContext, value) =>
            {
                var views = value.Right
                    .SelectMany(group => group)
                    .ToArray();

                if (views.Length == 0)
                {
                    return;
                }

                var result = PresentationViewManifestBuilder.Build(views);
                if (result.Diagnostics.Count > 0)
                {
                    foreach (var diagnostic in result.Diagnostics)
                    {
                        sourceContext.ReportDiagnostic(CreatePresentationDiagnostic(diagnostic, views));
                    }

                    return;
                }

                var source = PresentationViewRegistrarSourceBuilder.Build(result.Manifest);
                var assemblyName = string.IsNullOrWhiteSpace(value.Left.AssemblyName)
                    ? "Assembly"
                    : value.Left.AssemblyName!;

                sourceContext.AddSource(
                    GeneratedCodeNames.CreateHintName(GeneratorFeature.Presentation, assemblyName, "Views"),
                    SourceText.From(source, Encoding.UTF8));
            });
    }

    private static ModuleGenerationCandidate? ReadModuleCandidate(GeneratorSyntaxContext context)
    {
        var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;

        if (symbol is null)
        {
            return null;
        }

        var metadata = ModuleMetadataReader.TryRead(symbol);
        var isApplicationRoot = symbol
            .GetAttributes()
            .Any(attribute => string.Equals(
                attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                ApplicationModuleAttributeName,
                StringComparison.Ordinal));

        return metadata is null && !isApplicationRoot
            ? null
            : new ModuleGenerationCandidate(
                symbol,
                metadata,
                isApplicationRoot,
                symbol.Locations.FirstOrDefault());
    }

    private static bool CanGenerateFactory(INamedTypeSymbol symbol)
    {
        if (symbol.IsAbstract || symbol.IsStatic || symbol.Arity > 0 || !IsAccessible(symbol))
        {
            return false;
        }

        return symbol.InstanceConstructors.Any(constructor =>
            constructor.Parameters.Length == 0 &&
            constructor.DeclaredAccessibility is Accessibility.Public or
                Accessibility.Internal or
                Accessibility.ProtectedOrInternal);
    }

    private static bool IsAccessible(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return false;
            }

            if (current.DeclaredAccessibility is not Accessibility.Public and
                not Accessibility.Internal and
                not Accessibility.ProtectedOrInternal)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsExecutableOutput(OutputKind outputKind)
    {
        return outputKind is OutputKind.ConsoleApplication or
            OutputKind.WindowsApplication or
            OutputKind.WindowsRuntimeApplication;
    }

    private static void ReportInvalidGeneratedModule(
        SourceProductionContext sourceContext,
        ModuleGenerationCandidate candidate,
        string message)
    {
        sourceContext.ReportDiagnostic(GeneratorDiagnostics.CreateRoslynDiagnostic(
            GeneratorFeature.Modularity,
            new GeneratorDiagnostic(
                GeneratorDiagnostics.InvalidGeneratedModule,
                message,
                candidate.TypeName),
            candidate.Location));
    }

    private static ISet<string> GetAvailableExternalDependencyTypeNames(
        Compilation compilation,
        IReadOnlyList<CityModuleMetadata> modules)
    {
        var available = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dependency in modules.SelectMany(module => module.Dependencies))
        {
            var dependencyType = compilation.GetTypeByMetadataName(dependency.TypeName);

            if (dependencyType is not null &&
                !SymbolEqualityComparer.Default.Equals(
                    dependencyType.ContainingAssembly,
                    compilation.Assembly))
            {
                available.Add(dependency.TypeName);
            }
        }

        return available;
    }

    private static IReadOnlyList<string> ReadReferencedRegistrarTypeNames(Compilation compilation)
    {
        var registrarTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
            {
                continue;
            }

            foreach (var attribute in assembly.GetAttributes())
            {
                if (!string.Equals(
                        attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        GeneratedModuleManifestAttributeName,
                        StringComparison.Ordinal) ||
                    attribute.ConstructorArguments.Length == 0 ||
                    attribute.ConstructorArguments[0].Value is not INamedTypeSymbol registrarType ||
                    registrarType.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                registrarTypes.Add(registrarType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
            }
        }

        return registrarTypes.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<PresentationViewMetadata> ReadPresentationViews(GeneratorSyntaxContext context)
    {
        var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;

        return symbol is null ? [] : PresentationViewMetadataReader.Read(symbol);
    }

    private static Diagnostic CreatePresentationDiagnostic(
        GeneratorDiagnostic diagnostic,
        IReadOnlyList<PresentationViewMetadata> views)
    {
        return GeneratorDiagnostics.CreateRoslynDiagnostic(
            GeneratorFeature.Presentation,
            diagnostic,
            FindPresentationDiagnosticLocation(diagnostic, views));
    }

    private static Location? FindPresentationDiagnosticLocation(
        GeneratorDiagnostic diagnostic,
        IReadOnlyList<PresentationViewMetadata> views)
    {
        if (string.IsNullOrWhiteSpace(diagnostic.Target))
        {
            return null;
        }

        return views
            .FirstOrDefault(view =>
                string.Equals(view.ViewTypeName, diagnostic.Target, StringComparison.Ordinal) ||
                string.Equals(view.ViewModelTypeName, diagnostic.Target, StringComparison.Ordinal))
            ?.Location;
    }
}
