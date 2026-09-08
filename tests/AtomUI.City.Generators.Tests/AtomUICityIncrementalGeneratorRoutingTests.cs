using AtomUI.City.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AtomUI.City.Generators.Tests;

public sealed class AtomUICityIncrementalGeneratorRoutingTests
{
    [Fact]
    public void GeneratorEmitsRouteReferencesAndRuntimeManifest()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Routing;

            namespace Sample.App;

            public sealed class ShellViewModel;
            public sealed class DetailsViewModel;
            public readonly record struct DetailsParameters(int Id);

            [RouteMap]
            public static partial class AppRoutes
            {
                [LayoutRoute(typeof(ShellViewModel), Id = "app.shell")]
                public static partial RouteReference Shell();

                [Route("details/{id:int}", typeof(DetailsViewModel), Id = "app.details", Parent = nameof(Shell))]
                public static partial RouteReference<DetailsParameters> Details();
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);
        var generated = Assert.Single(RoutingSources(Assert.Single(driver.GetRunResult().Results)));
        var source = generated.SourceText.ToString();

        Assert.Empty(generatorDiagnostics);
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains("RouteReference Shell()", source, StringComparison.Ordinal);
        Assert.Contains("RouteReference<global::Sample.App.DetailsParameters>", source, StringComparison.Ordinal);
        Assert.Contains("parameters.Id", source, StringComparison.Ordinal);
        Assert.Contains("GeneratedRoutingRouteManifest", source, StringComparison.Ordinal);
        Assert.Contains("CreateSnapshot", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorReportsRouteManifestConflict()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Routing;

            namespace Sample.App;

            public sealed class ViewModel;

            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("settings", typeof(ViewModel), Id = "duplicate")]
                public static partial RouteReference Settings();

                [Route("other", typeof(ViewModel), Id = "duplicate")]
                public static partial RouteReference Other();
            }
            """);
        var driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        var result = driver.RunGenerators(compilation).GetRunResult();
        var generatorResult = Assert.Single(result.Results);

        Assert.Empty(RoutingSources(generatorResult));
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN004");
    }

    [Theory]
    [InlineData("public class AppRoutes", "public static partial RouteReference Home()")]
    [InlineData("public static partial class AppRoutes", "public static RouteReference Home() => new(\"home\")")]
    [InlineData("public static partial class AppRoutes", "public static partial string Home()")]
    public void GeneratorRejectsInvalidRouteMapAndMethodShapes(string mapDeclaration, string methodDeclaration)
    {
        var compilation = CreateCompilation(
            $$"""
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            {{mapDeclaration}}
            {
                [Route("home", typeof(ViewModel), Id = "home")]
                {{methodDeclaration}};
            }
            """);

        var result = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        Assert.Empty(RoutingSources(Assert.Single(result.Results)));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
    }

    [Fact]
    public void GeneratorEmitsDeclaredRouteBehaviors()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            public sealed class Guard : IRouteEnterGuard
            {
                public ValueTask<RouteGuardResult> CanEnterAsync(RouteGuardContext context, CancellationToken token) =>
                    ValueTask.FromResult(RouteGuardResult.Allow());
            }
            public sealed class Resolver : IRouteResolver
            {
                public ValueTask<RouteResolveResult> ResolveAsync(RouteResolveContext context, CancellationToken token) =>
                    ValueTask.FromResult(RouteResolveResult.Success());
            }
            public sealed class Middleware : IRouteNavigationMiddleware
            {
                public ValueTask<NavigationResult> InvokeAsync(RouteNavigationMiddlewareContext context, RouteNavigationDelegate next, CancellationToken token) => next();
            }
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("home", typeof(ViewModel), Id = "home")]
                [RouteGuards(typeof(Guard))]
                [RouteResolvers(typeof(Resolver))]
                [RouteMiddleware(typeof(Middleware))]
                public static partial RouteReference Home();
            }
            """);

        var result = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator())
            .RunGenerators(compilation)
            .GetRunResult();
        var source = Assert.Single(RoutingSources(Assert.Single(result.Results))).SourceText.ToString();

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains("enterGuardTypes: new global::System.Type[]", source, StringComparison.Ordinal);
        Assert.Contains("resolverTypes: new global::System.Type[]", source, StringComparison.Ordinal);
        Assert.Contains("middlewareTypes: new global::System.Type[]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorEmitsExtensionPointAndQueryFragmentBindings()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class SearchViewModel;
            public sealed record SearchParameters
            {
                public required string Term { get; init; }
                [Query] public int Page { get; init; }
                [Fragment] public string? Section { get; init; }
            }
            [RouteMap]
            public static partial class AppRoutes
            {
                [RouteExtensionPoint("search.pages", Id = "search.pages")]
                public static partial RouteExtensionPoint SearchPages();

                [Route("search/{term}", typeof(SearchViewModel), Id = "search")]
                public static partial RouteReference<SearchParameters> Search();
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);
        var source = Assert.Single(RoutingSources(Assert.Single(driver.GetRunResult().Results))).SourceText.ToString();

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains("RouteExtensionPoint SearchPages()", source, StringComparison.Ordinal);
        Assert.Contains("parameters.Page", source, StringComparison.Ordinal);
        Assert.Contains("parameters.Section", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorRejectsUnknownTemplateConstraint()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("items/{id:unknown}", typeof(ViewModel), Id = "items")]
                public static partial RouteReference Items();
            }
            """);

        Assert.Empty(RoutingSources(Assert.Single(result.Results)));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
    }

    [Fact]
    public void GeneratorRejectsParentCycle()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes
            {
                [LayoutRoute(typeof(ViewModel), Id = "a", Parent = nameof(B))]
                public static partial RouteReference A();
                [LayoutRoute(typeof(ViewModel), Id = "b", Parent = nameof(A))]
                public static partial RouteReference B();
            }
            """);

        Assert.Empty(RoutingSources(Assert.Single(result.Results)));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
    }

    [Fact]
    public void GeneratorIgnoresIndexersAndWriteOnlyPropertiesForParameterBinding()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            public sealed class Parameters
            {
                public int Id { get; init; }
                [Query] public int WriteOnly { set { } }
                [Query] public int this[int index] => index;
            }
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("items/{id:int}", typeof(ViewModel), Id = "items")]
                public static partial RouteReference<Parameters> Items();
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);
        var source = Assert.Single(RoutingSources(Assert.Single(driver.GetRunResult().Results))).SourceText.ToString();

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains("parameters.Id", source, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters.WriteOnly", source, StringComparison.Ordinal);
        Assert.DoesNotContain("parameters.Item", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorReportsCaseInsensitiveParameterMemberAmbiguityWithoutCrashing()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            public sealed class Parameters
            {
                public int Id;
                public int id;
            }
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("items/{id:int}", typeof(ViewModel), Id = "items")]
                public static partial RouteReference<Parameters> Items();
            }
            """);

        var generatorResult = Assert.Single(result.Results);
        Assert.Empty(RoutingSources(generatorResult));
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
        Assert.Null(generatorResult.Exception);
    }

    [Fact]
    public void GeneratorEscapesKeywordMethodAndParameterMemberNames()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            public sealed class Parameters
            {
                public int @class { get; init; }
            }
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("items/{class:int}", typeof(ViewModel), Id = "items")]
                public static partial RouteReference<Parameters> @event();
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);
        var source = Assert.Single(RoutingSources(Assert.Single(driver.GetRunResult().Results))).SourceText.ToString();

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains("@event()", source, StringComparison.Ordinal);
        Assert.Contains("parameters.@class", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorDeduplicatesAttributedPartialRouteMapCandidates()
    {
        var compilation = CreateCompilation(
            """
            using System;
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("home", typeof(ViewModel), Id = "home")]
                public static partial RouteReference Home();
            }
            [Obsolete]
            public static partial class AppRoutes
            {
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Single(RoutingSources(Assert.Single(driver.GetRunResult().Results)));
    }

    [Fact]
    public void GeneratorAllowsTemplateCandidatesDisambiguatedByMatchPolicy()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            public sealed class Policy : IRouteMatchPolicy
            {
                public ValueTask<bool> CanMatchAsync(RouteMatchPolicyContext context, CancellationToken token) =>
                    ValueTask.FromResult(true);
            }
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("shared", typeof(ViewModel), Id = "first")]
                [RouteMatchPolicies(typeof(Policy))]
                public static partial RouteReference First();
                [Route("shared", typeof(ViewModel), Id = "second")]
                public static partial RouteReference Second();
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Single(RoutingSources(Assert.Single(driver.GetRunResult().Results)));
    }

    [Fact]
    public void GeneratorRejectsOverlappingOptionalAndDefaultTemplateParameters()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("items/{id}", typeof(ViewModel), Id = "required")]
                public static partial RouteReference Required();
                [Route("items/{id?}", typeof(ViewModel), Id = "optional")]
                public static partial RouteReference Optional();
                [Route("items/{id=all}", typeof(ViewModel), Id = "default")]
                public static partial RouteReference Default();
            }
            """);

        var generatorResult = Assert.Single(result.Results);
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN004");
        Assert.Empty(RoutingSources(generatorResult));
    }

    [Fact]
    public void GeneratorBindsInheritedPublicParameterMembers()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            public class BaseParameters
            {
                public int Id { get; init; }
            }
            public sealed class Parameters : BaseParameters;
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("items/{id:int}", typeof(ViewModel), Id = "items")]
                public static partial RouteReference<Parameters> Items();
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);
        var result = Assert.Single(driver.GetRunResult().Results);
        var source = Assert.Single(RoutingSources(result)).SourceText.ToString();

        Assert.Null(result.Exception);
        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains("parameters.Id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorReportsBlankRouteIdentityWithoutCrashing()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("home", typeof(ViewModel), Id = " ", Outlet = "")]
                public static partial RouteReference Home();
            }
            """);
        var generatorResult = Assert.Single(result.Results);

        Assert.Null(generatorResult.Exception);
        Assert.Empty(RoutingSources(generatorResult));
        Assert.Equal(2, generatorResult.Diagnostics.Count(diagnostic => diagnostic.Id == "AUCGEN005"));
    }

    [Fact]
    public void GeneratorRejectsDefaultValueThatViolatesTemplateConstraint()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("items/{id:int=not-an-int}", typeof(ViewModel), Id = "items")]
                public static partial RouteReference Items();
            }
            """);
        var generatorResult = Assert.Single(result.Results);

        Assert.Null(generatorResult.Exception);
        Assert.Empty(RoutingSources(generatorResult));
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
    }

    [Fact]
    public void GeneratorOutputUsesDeterministicLfLineEndings()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("home", typeof(ViewModel), Id = "home")]
                public static partial RouteReference Home();
            }
            """);

        var source = Assert.Single(RoutingSources(Assert.Single(result.Results))).SourceText.ToString();
        Assert.DoesNotContain('\r', source);
    }

    [Fact]
    public void GeneratorUsesStableExtensionPointIdForGeneratedReference()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            [RouteMap]
            public static partial class AppRoutes
            {
                [RouteExtensionPoint("slot.id", Id = "descriptor.id")]
                public static partial RouteExtensionPoint Slot();
            }
            """);

        var generatorResult = Assert.Single(result.Results);
        var source = Assert.Single(RoutingSources(generatorResult)).SourceText.ToString();

        Assert.Empty(generatorResult.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains("RouteExtensionPoint(@\"slot.id\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RouteExtensionPoint(@\"descriptor.id\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorRejectsOpenGenericViewModelAndBehaviorTypes()
    {
        var result = Run(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class OpenViewModel<T>;
            public sealed class OpenGuard<T> : IRouteEnterGuard
            {
                public ValueTask<RouteGuardResult> CanEnterAsync(RouteGuardContext context, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(RouteGuardResult.Allow());
            }
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("open", typeof(OpenViewModel<>), Id = "open")]
                [RouteGuards(typeof(OpenGuard<>))]
                public static partial RouteReference Open();
            }
            """);

        var generatorResult = Assert.Single(result.Results);
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
        Assert.Empty(RoutingSources(generatorResult));
    }

    [Fact]
    public void GeneratorRejectsGenericRouteMap()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes<T>
            {
                [Route("home", typeof(ViewModel), Id = "home")]
                public static partial RouteReference Home();
            }
            """);

        var generatorResult = Assert.Single(result.Results);
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
        Assert.Empty(RoutingSources(generatorResult));
    }

    [Fact]
    public void GeneratorRejectsGenericRouteMethodAndHandwrittenImplementation()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("generic", typeof(ViewModel), Id = "generic")]
                public static partial RouteReference Generic<T>();

                [Route("manual", typeof(ViewModel), Id = "manual")]
                public static partial RouteReference Manual();
                public static partial RouteReference Manual() => new("manual");
            }
            """);

        var generatorResult = Assert.Single(result.Results);
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
        Assert.Empty(RoutingSources(generatorResult));
    }

    [Fact]
    public void GeneratorRejectsMultipleRouteDefinitionAttributesOnOneMethod()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route("home", typeof(ViewModel), Id = "home")]
                [RedirectRoute("legacy", Target = nameof(Home), Id = "legacy")]
                public static partial RouteReference Home();
            }
            """);

        var generatorResult = Assert.Single(result.Results);
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
        Assert.Empty(RoutingSources(generatorResult));
    }

    [Fact]
    public void GeneratorRejectsRedirectToNonNavigableTarget()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            [RouteMap]
            public static partial class AppRoutes
            {
                [RouteGroup("account", Id = "account")]
                public static partial RouteReference Account();
                [RedirectRoute("legacy", Target = nameof(Account), Id = "legacy")]
                public static partial RouteReference Legacy();
            }
            """);

        var generatorResult = Assert.Single(result.Results);
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
        Assert.Empty(RoutingSources(generatorResult));
    }

    [Fact]
    public void GeneratorRejectsEquivalentEffectiveTemplatesAcrossHierarchies()
    {
        var result = Run(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes
            {
                [RouteGroup("account", Id = "account")]
                public static partial RouteReference Account();
                [Route("settings", typeof(ViewModel), Parent = nameof(Account), Id = "nested")]
                public static partial RouteReference Nested();
                [Route("account/settings", typeof(ViewModel), Id = "flat")]
                public static partial RouteReference Flat();
            }
            """);

        var generatorResult = Assert.Single(result.Results);
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN004");
        Assert.Empty(RoutingSources(generatorResult));
    }

    [Fact]
    public void GeneratorAcceptsRegexWithEscapedParenthesesAndCommaQuantifier()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Routing;
            namespace Sample.App;
            public sealed class ViewModel;
            [RouteMap]
            public static partial class AppRoutes
            {
                [Route(@"value/{code:regex(^\(a{1,3}\)$)}", typeof(ViewModel), Id = "value")]
                public static partial RouteReference Value();
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);
        var generatorResult = Assert.Single(driver.GetRunResult().Results);

        Assert.Empty(generatorResult.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Single(RoutingSources(generatorResult));
    }

    private static GeneratorDriverRunResult Run(string source) =>
        CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator())
            .RunGenerators(CreateCompilation(source))
            .GetRunResult();

    private static IEnumerable<GeneratedSourceResult> RoutingSources(GeneratorRunResult result) =>
        result.GeneratedSources.Where(source => source.HintName.Contains("/Routing/", StringComparison.Ordinal));

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Concat([MetadataReference.CreateFromFile(typeof(AtomUI.City.Routing.RouteReference).Assembly.Location)])
            .DistinctBy(reference => reference.Display)
            .ToArray();

        return CSharpCompilation.Create(
            "Sample.App",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
