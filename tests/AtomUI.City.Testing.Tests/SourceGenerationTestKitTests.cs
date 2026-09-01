using System.Text;

using AtomUI.City.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AtomUI.City.Testing.Tests;

public sealed class SourceGenerationTestKitTests
{
    [Fact]
    public void GeneratedSourceSnapshotOrdersSourcesAndNormalizesLineEndings()
    {
        var snapshot = GeneratedSourceSnapshot.Create(
            [
                new GeneratedSource("B.g.cs", "namespace B\r\n{\r\n}\r\n"),
                new GeneratedSource("A.g.cs", "namespace A\n{\n}\n"),
            ]);

        Assert.Equal(
            """
            // <generated-source hint="A.g.cs">
            namespace A
            {
            }
            // </generated-source>
            // <generated-source hint="B.g.cs">
            namespace B
            {
            }
            // </generated-source>
            """,
            snapshot.Text);
    }

    [Fact]
    public void SourceGenerationTestCaseStoresCompilationInputsAndExpectedDiagnostics()
    {
        var testCase = SourceGenerationTestCase
            .Create("module manifest")
            .AddSource("Module.cs", "public sealed class TestModule {}")
            .ExpectDiagnostic("AUCGEN002");

        Assert.Equal("module manifest", testCase.Name);
        Assert.Collection(testCase.Sources, source => Assert.Equal("Module.cs", source.Path));
        Assert.Collection(testCase.ExpectedDiagnostics, diagnostic => Assert.Equal("AUCGEN002", diagnostic.Id));
    }

    [Fact]
    public void SourceGenerationTestCaseCollectionsRejectExternalMutation()
    {
        var testCase = SourceGenerationTestCase
            .Create("module manifest")
            .AddSource("Module.cs", "public sealed class TestModule {}")
            .ExpectDiagnostic("AUCGEN002");

        var sources = Assert.IsAssignableFrom<IList<SourceFile>>(testCase.Sources);
        var expectedDiagnostics = Assert.IsAssignableFrom<IList<ExpectedDiagnostic>>(testCase.ExpectedDiagnostics);

        Assert.Throws<NotSupportedException>(() => sources.Add(new SourceFile("Other.cs", "public sealed class Other {}")));
        Assert.Throws<NotSupportedException>(() => expectedDiagnostics.Add(new ExpectedDiagnostic("AUCGEN002")));
        Assert.Single(testCase.Sources);
        Assert.Single(testCase.ExpectedDiagnostics);
    }

    [Fact]
    public void RunExecutesSourceGeneratorAndReturnsStableSnapshot()
    {
        var result = SourceGenerationTestCase
            .Create("hello generator")
            .AddSource("Input.cs", "namespace Input { public sealed class Marker {} }")
            .Run(new HelloGenerator());

        Assert.Contains("// <generated-source hint=\"Hello.g.cs\">", result.Snapshot.Text, StringComparison.Ordinal);
        Assert.Contains("public sealed class Hello", result.Snapshot.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task RunObservesCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var testCase = SourceGenerationTestCase
            .Create("hello generator")
            .AddSource("Input.cs", "namespace Input { public sealed class Marker {} }");

        Assert.Throws<OperationCanceledException>(() => testCase.Run(new HelloGenerator(), cancellation.Token));
    }

#pragma warning disable RS1042
    private sealed class HelloGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource(
                "Hello.g.cs",
                SourceText.From(
                    "namespace Generated { public sealed class Hello {} }",
                    Encoding.UTF8));
        }
    }
#pragma warning restore RS1042
}
