using AtomUI.City.Testing;

namespace AtomUI.City.Testing.Tests;

public sealed class AotCompatibilityCheckTests
{
    [Fact]
    public void EvaluateReportsForbiddenRuntimeReflectionPatterns()
    {
        var check = AotCompatibilityCheck
            .Create()
            .ForbidPattern("AOT001", "Assembly.GetTypes");

        var diagnostics = check.Evaluate(
            [
                new SourceFile("ModuleScanner.cs", "var types = assembly.Assembly.GetTypes();"),
                new SourceFile("StaticManifest.cs", "var manifest = StaticManifest.Instance;"),
            ]);

        var diagnostic = Assert.Single(diagnostics);

        Assert.Equal("AOT001", diagnostic.Id);
        Assert.Equal("ModuleScanner.cs", diagnostic.SourcePath);
        Assert.Contains("Assembly.GetTypes", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluateReturnsNoDiagnosticsWhenSourcesAvoidForbiddenPatterns()
    {
        var check = AotCompatibilityCheck
            .Create()
            .ForbidPattern("AOT001", "Assembly.GetTypes");

        var diagnostics = check.Evaluate([new SourceFile("StaticManifest.cs", "var manifest = StaticManifest.Instance;")]);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ForbidDefaultAotPatternsReportsReflectionActivatorAndDynamicCode()
    {
        var check = AotCompatibilityCheck
            .Create()
            .ForbidDefaultAotPatterns();

        var diagnostics = check.Evaluate(
            [
                new SourceFile("Reflection.cs", "var types = assembly.Assembly.GetTypes();"),
                new SourceFile("Activator.cs", "var instance = Activator.CreateInstance(type);"),
                new SourceFile("DynamicCode.cs", "var method = new DynamicMethod(\"m\", null, Type.EmptyTypes);"),
            ]);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "AOT001");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "AOT002");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "AOT003");
    }

    [Fact]
    public async Task EvaluateObservesCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var check = AotCompatibilityCheck
            .Create()
            .ForbidPattern("AOT001", "Assembly.GetTypes");

        Assert.Throws<OperationCanceledException>(() => check.Evaluate(
            [new SourceFile("Reflection.cs", "var types = assembly.Assembly.GetTypes();")],
            cancellation.Token));
    }

    [Fact]
    public void EvaluateDiagnosticsRejectExternalListMutation()
    {
        var check = AotCompatibilityCheck
            .Create()
            .ForbidPattern("AOT001", "Assembly.GetTypes");

        var diagnostics = check.Evaluate(
            [new SourceFile("ModuleScanner.cs", "var types = assembly.Assembly.GetTypes();")]);
        var exposedDiagnostics = Assert.IsAssignableFrom<IList<AotCompatibilityDiagnostic>>(diagnostics);

        Assert.Throws<NotSupportedException>(() => exposedDiagnostics[0] = new AotCompatibilityDiagnostic(
            "AOT999",
            "Changed.cs",
            "Changed"));
        Assert.Equal("AOT001", diagnostics[0].Id);
    }
}
