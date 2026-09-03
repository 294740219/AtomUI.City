namespace AtomUI.City.Core.Modularity;

internal sealed class ValidatedModuleGraph
{
    internal ValidatedModuleGraph(IReadOnlyList<ModuleRegistration> orderedRegistrations)
    {
        ArgumentNullException.ThrowIfNull(orderedRegistrations);
        OrderedRegistrations = Array.AsReadOnly(orderedRegistrations.ToArray());
    }

    public IReadOnlyList<ModuleRegistration> OrderedRegistrations { get; }
}
