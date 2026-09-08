using System.Text;

namespace AtomUI.City.Testing;

public sealed class GeneratedSourceSnapshot
{
    private GeneratedSourceSnapshot(string text)
    {
        Text = text;
    }

    public string Text { get; }

    public static GeneratedSourceSnapshot Create(IEnumerable<GeneratedSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var builder = new StringBuilder();

        foreach (var source in sources.OrderBy(source => source.HintName, StringComparer.Ordinal))
        {
            builder.Append("// <generated-source hint=\"");
            builder.Append(source.HintName);
            builder.Append("\">\n");
            builder.Append(NormalizeSourceText(source.SourceText));
            builder.Append("\n// </generated-source>\n");
        }

        return new GeneratedSourceSnapshot(builder.ToString().TrimEnd());
    }

    private static string NormalizeSourceText(string sourceText)
    {
        return sourceText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd();
    }
}
