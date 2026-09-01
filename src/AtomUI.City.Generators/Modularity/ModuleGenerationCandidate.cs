using Microsoft.CodeAnalysis;

namespace AtomUI.City.Generators.Modularity;

internal sealed class ModuleGenerationCandidate
{
    public ModuleGenerationCandidate(
        INamedTypeSymbol symbol,
        ModuleMetadata? metadata,
        bool isApplicationRoot,
        Location? location)
    {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        Metadata = metadata;
        IsApplicationRoot = isApplicationRoot;
        Location = location;
    }

    public INamedTypeSymbol Symbol { get; }

    public ModuleMetadata? Metadata { get; }

    public bool IsApplicationRoot { get; }

    public string TypeName => Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    public Location? Location { get; }
}
