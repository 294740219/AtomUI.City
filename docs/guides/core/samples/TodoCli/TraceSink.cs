using AtomUI.City.Core.DependencyInjection;

namespace TodoCli;

/// <summary>
/// Demonstrates marker-interface registration. This class implements
/// <see cref="ITransientDependency"/> but exposes no custom interface, so the
/// generator registers it as its concrete type <see cref="TraceSink"/>.
/// </summary>
public sealed class TraceSink : ITransientDependency
{
    private readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines => _lines;

    public void Write(string line) => _lines.Add(line);
}
