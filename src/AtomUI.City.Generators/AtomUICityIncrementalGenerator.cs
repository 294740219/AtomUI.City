using System.Text;
using AtomUI.City.Generators.Common;
using AtomUI.City.Generators.Diagnostics;
using AtomUI.City.Generators.Presentation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AtomUI.City.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class AtomUICityIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
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
                        sourceContext.ReportDiagnostic(CreateDiagnostic(GeneratorFeature.Presentation, diagnostic, views));
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

    private static IReadOnlyList<PresentationViewMetadata> ReadPresentationViews(GeneratorSyntaxContext context)
    {
        var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node) as INamedTypeSymbol;

        return symbol is null ? [] : PresentationViewMetadataReader.Read(symbol);
    }

    private static Diagnostic CreateDiagnostic(
        GeneratorFeature feature,
        GeneratorDiagnostic diagnostic,
        IReadOnlyList<PresentationViewMetadata> views)
    {
        return GeneratorDiagnostics.CreateRoslynDiagnostic(
            feature,
            diagnostic,
            FindDiagnosticLocation(diagnostic, views));
    }

    private static Location? FindDiagnosticLocation(
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
