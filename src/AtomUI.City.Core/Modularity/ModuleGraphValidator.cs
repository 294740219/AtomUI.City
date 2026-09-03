namespace AtomUI.City.Core.Modularity;

internal static class ModuleGraphValidator
{
    public static ValidatedModuleGraph Validate(IReadOnlyList<ModuleRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var registrationsByType = new Dictionary<Type, ModuleRegistration>();
        var moduleTypesByName = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var registration in registrations)
        {
            ArgumentNullException.ThrowIfNull(registration);
            var descriptor = GetDescriptor(registration);

            if (descriptor.ModuleType != registration.ModuleType)
            {
                throw new InvalidOperationException(
                    $"Module registration type '{registration.ModuleType.FullName}' does not match descriptor type '{descriptor.ModuleType.FullName}'.");
            }

            if (!registrationsByType.TryAdd(registration.ModuleType, registration))
            {
                throw new InvalidOperationException(
                    $"Module type '{registration.ModuleType.FullName}' is registered more than once.");
            }

            if (moduleTypesByName.TryGetValue(descriptor.Name, out var existingType))
            {
                throw new InvalidOperationException(
                    $"Duplicate module id '{descriptor.Name}' declared by '{existingType.FullName}' and '{descriptor.ModuleType.FullName}'.");
            }

            moduleTypesByName.Add(descriptor.Name, descriptor.ModuleType);
        }

        var ordered = new List<ModuleRegistration>(registrations.Count);
        var visitStates = new Dictionary<Type, ModuleVisitState>();
        var path = new List<Type>();

        foreach (var registration in registrations)
        {
            Visit(registration, registrationsByType, visitStates, ordered, path);
        }

        return new ValidatedModuleGraph(ordered);
    }

    private static void Visit(
        ModuleRegistration registration,
        IReadOnlyDictionary<Type, ModuleRegistration> registrationsByType,
        IDictionary<Type, ModuleVisitState> visitStates,
        ICollection<ModuleRegistration> ordered,
        List<Type> path)
    {
        if (visitStates.TryGetValue(registration.ModuleType, out var state))
        {
            if (state == ModuleVisitState.Visited)
            {
                return;
            }

            var cycleStart = path.FindIndex(type => type == registration.ModuleType);
            var cycle = path
                .Skip(cycleStart < 0 ? 0 : cycleStart)
                .Append(registration.ModuleType)
                .Select(type => type.FullName);

            throw new InvalidOperationException(
                $"Module dependency graph contains a cycle: {string.Join(" -> ", cycle)}.");
        }

        visitStates.Add(registration.ModuleType, ModuleVisitState.Visiting);
        path.Add(registration.ModuleType);
        var descriptor = GetDescriptor(registration);

        foreach (var dependency in descriptor.Dependencies)
        {
            if (!registrationsByType.TryGetValue(dependency.ModuleType, out var dependencyRegistration))
            {
                if (dependency.Optional)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Module '{descriptor.ModuleType.FullName}' depends on missing module '{dependency.ModuleType.FullName}'.");
            }

            Visit(dependencyRegistration, registrationsByType, visitStates, ordered, path);
        }

        path.RemoveAt(path.Count - 1);
        visitStates[registration.ModuleType] = ModuleVisitState.Visited;
        ordered.Add(registration);
    }

    private static ModuleDescriptor GetDescriptor(ModuleRegistration registration)
    {
        return registration.Descriptor
            ?? throw new InvalidOperationException(
                $"Module '{registration.ModuleType.FullName}' does not have a resolved descriptor.");
    }

    private enum ModuleVisitState
    {
        Visiting,
        Visited,
    }
}
