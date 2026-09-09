using AtomUI.City.Data;

namespace AtomUI.City.Data.Tests;

public sealed class DataDiagnosticsTests
{
    [Fact]
    public void DiagnosticCatalogUsesUniqueWellFormedCodes()
    {
        var codes = typeof(DataDiagnosticIds)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(static field => Assert.IsType<string>(field.GetRawConstantValue()))
            .ToArray();

        Assert.NotEmpty(codes);
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, static code => Assert.Matches("^AUCDATA[0-9]{3}$", code));
    }

    [Fact]
    public void RecordsSnapshotRejectsExternalListMutation()
    {
        var diagnostics = new InMemoryDataDiagnostics();
        diagnostics.Write(new DataDiagnosticRecord("AUCDATA999", "First", DataDiagnosticSeverity.Info));
        var records = Assert.IsAssignableFrom<IList<DataDiagnosticRecord>>(diagnostics.Records);

        Assert.Throws<NotSupportedException>(() => records[0] = new DataDiagnosticRecord(
            "AUCDATA998",
            "Changed",
            DataDiagnosticSeverity.Error));
        Assert.Equal("AUCDATA999", diagnostics.Records[0].Code);
    }

    [Fact]
    public void DiagnosticRecordRejectsInvalidMetadata()
    {
        Assert.Throws<ArgumentException>(() =>
            new DataDiagnosticRecord(" ", "message", DataDiagnosticSeverity.Info));
        Assert.Throws<ArgumentException>(() =>
            new DataDiagnosticRecord("AUCDATA999", " ", DataDiagnosticSeverity.Info));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataDiagnosticRecord("AUCDATA999", "message", (DataDiagnosticSeverity)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DataDiagnosticRecord("AUCDATA999", "message", DataDiagnosticSeverity.Info, Attempt: -1));
        Assert.Throws<ArgumentException>(() =>
            new DataDiagnosticRecord(
                "AUCDATA999",
                "message",
                DataDiagnosticSeverity.Info,
                OperationId: Guid.Empty));
        Assert.Throws<ArgumentException>(() =>
            new DataDiagnosticRecord(
                "AUCDATA999",
                "message",
                DataDiagnosticSeverity.Info,
                ClientId: " "));
    }

    [Fact]
    public void InMemoryDiagnosticsUsesBoundedOldestFirstEviction()
    {
        var diagnostics = new InMemoryDataDiagnostics(2);

        diagnostics.Write(new DataDiagnosticRecord("AUCDATA901", "first", DataDiagnosticSeverity.Info));
        diagnostics.Write(new DataDiagnosticRecord("AUCDATA902", "second", DataDiagnosticSeverity.Info));
        diagnostics.Write(new DataDiagnosticRecord("AUCDATA903", "third", DataDiagnosticSeverity.Info));

        Assert.Equal(2, diagnostics.Capacity);
        Assert.Equal(1, diagnostics.DroppedCount);
        Assert.Equal(["AUCDATA902", "AUCDATA903"], diagnostics.Records.Select(static record => record.Code));
    }
}
