using AtomUI.City.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AtomUI.City.Generators.Tests;

public sealed class AtomUICityIncrementalGeneratorDataTests
{
    [Fact]
    public void GeneratorEmitsStrongTypedDataClientDescriptorCatalog()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using AtomUI.City.Data;

            namespace Sample.App;

            public sealed record Query(string Term);
            public sealed record Item(string Name);

            [DataClient("catalog", DataTransportKind.Http, Version = "2")]
            public interface ICatalogClient
            {
                [DataOperation(
                    "search",
                    DataAccessMode.Query,
                    ConcurrencyPolicy = DataConcurrencyPolicy.LatestWins,
                    TimeoutMilliseconds = 5000,
                    MaxRetryAttempts = 2,
                    CacheEnabled = true,
                    AuthenticationPolicy = "Bearer")]
                ValueTask<DataResult<Item>> SearchAsync(Query query, CancellationToken cancellationToken);
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var output,
            out var diagnostics);
        var generated = Assert.Single(Assert.Single(driver.GetRunResult().Results).GeneratedSources
            .Where(source => source.HintName.Contains("/Data/", StringComparison.Ordinal)));
        var source = generated.SourceText.ToString();

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains("GeneratedDataClientManifestAttribute", source, StringComparison.Ordinal);
        Assert.Contains("typeof(global::Sample.App.ICatalogClient)", source, StringComparison.Ordinal);
        Assert.Contains("typeof(global::Sample.App.Query)", source, StringComparison.Ordinal);
        Assert.Contains("typeof(global::Sample.App.Item)", source, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMilliseconds(5000)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorRejectsDuplicateClientIds()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Data;
            namespace Sample.App;
            [DataClient("duplicate", DataTransportKind.Http)] public interface IFirst;
            [DataClient("duplicate", DataTransportKind.Grpc)] public interface ISecond;
            """);

        var result = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator())
            .RunGenerators(compilation)
            .GetRunResult();
        var generatorResult = Assert.Single(result.Results);

        Assert.Empty(generatorResult.GeneratedSources.Where(source => source.HintName.Contains("/Data/", StringComparison.Ordinal)));
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
    }

    [Fact]
    public void GeneratorRejectsAmbiguousOperationSignatureAndMutationCache()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using AtomUI.City.Data;
            namespace Sample.App;
            [DataClient("invalid", DataTransportKind.Http)]
            public interface IInvalidClient
            {
                [DataOperation("save", DataAccessMode.Mutation, CacheEnabled = true)]
                ValueTask<DataResult<string>> SaveAsync(string first, string second, CancellationToken token);
            }
            """);

        var result = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator())
            .RunGenerators(compilation)
            .GetRunResult();
        var generatorResult = Assert.Single(result.Results);

        Assert.Empty(generatorResult.GeneratedSources.Where(source => source.HintName.Contains("/Data/", StringComparison.Ordinal)));
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
    }

    [Fact]
    public void GeneratorEmitsCompilableTupleTypeReferences()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using AtomUI.City.Data;
            namespace Sample.App;
            [DataClient("tuple", DataTransportKind.Http)]
            public interface ITupleClient
            {
                [DataOperation("query")]
                ValueTask<DataResult<(string Name, int Count)>> QueryAsync((string Term, int Page) request);
            }
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var source = Assert.Single(Assert.Single(driver.GetRunResult().Results).GeneratedSources
            .Where(item => item.HintName.Contains("/Data/", StringComparison.Ordinal)))
            .SourceText.ToString();
        Assert.Contains("typeof((string Term, int Page))", source, StringComparison.Ordinal);
        Assert.Contains("typeof((string Name, int Count))", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratorRejectsClientNestedInGenericType()
    {
        var compilation = CreateCompilation(
            """
            using AtomUI.City.Data;
            namespace Sample.App;
            public sealed class Container<T>
            {
                [DataClient("nested", DataTransportKind.Http)]
                public interface INestedClient;
            }
            """);

        var result = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator())
            .RunGenerators(compilation)
            .GetRunResult();
        var generatorResult = Assert.Single(result.Results);

        Assert.Empty(generatorResult.GeneratedSources.Where(source => source.HintName.Contains("/Data/", StringComparison.Ordinal)));
        Assert.Contains(generatorResult.Diagnostics, diagnostic => diagnostic.Id == "AUCGEN005");
    }

    [Fact]
    public void GeneratorIncludesOperationsInheritedByDataClientInterface()
    {
        var compilation = CreateCompilation(
            """
            using System.Threading.Tasks;
            using AtomUI.City.Data;
            namespace Sample.App;
            public interface IBaseClient
            {
                [DataOperation("inherited")]
                ValueTask<DataResult<string>> GetAsync(int request);
            }

            [DataClient("derived", DataTransportKind.Http)]
            public interface IDerivedClient : IBaseClient;
            """);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AtomUICityIncrementalGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var source = Assert.Single(Assert.Single(driver.GetRunResult().Results).GeneratedSources
            .Where(item => item.HintName.Contains("/Data/", StringComparison.Ordinal)))
            .SourceText.ToString();
        Assert.Contains("@\"inherited\"", source, StringComparison.Ordinal);
        Assert.Contains("typeof(int)", source, StringComparison.Ordinal);
        Assert.Contains("typeof(string)", source, StringComparison.Ordinal);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Concat([MetadataReference.CreateFromFile(typeof(AtomUI.City.Data.DataClientAttribute).Assembly.Location)])
            .DistinctBy(reference => reference.Display)
            .ToArray();

        return CSharpCompilation.Create(
            "Sample.App",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
