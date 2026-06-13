using AtomUI.City.Modularity;

namespace AtomUI.City.Testing;

public sealed class ModuleTestHostBuilder
{
    private readonly List<ModuleTestRecord> _modules = [];
    private readonly TestHostBuilder _hostBuilder = TestHost.CreateBuilder();
    private bool _built;

    public ModuleTestHostBuilder UseModule(string name, IModule module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(module);
        ThrowIfBuilt();

        _modules.Add(new ModuleTestRecord(name, module));

        return this;
    }

    public ModuleTestHostBuilder UseHostProperty(string key, object? value)
    {
        ThrowIfBuilt();

        _hostBuilder.UseProperty(key, value);

        return this;
    }

    public ModuleTestHost Build()
    {
        ThrowIfBuilt();

        var orderedModules = OrderByDependencies(_modules);
        var host = _hostBuilder.Build();
        _built = true;

        return new ModuleTestHost(host, orderedModules);
    }

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("The module test host builder has already built a host and is frozen.");
        }
    }

    private static IReadOnlyList<ModuleTestRecord> OrderByDependencies(IReadOnlyList<ModuleTestRecord> modules)
    {
        var duplicateName = modules
            .GroupBy(module => module.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateName is not null)
        {
            throw new InvalidOperationException($"Duplicate module name '{duplicateName.Key}'.");
        }

        var duplicateType = modules
            .GroupBy(module => module.Module.GetType())
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateType is not null)
        {
            throw new InvalidOperationException($"Duplicate module type '{duplicateType.Key.FullName}'.");
        }

        var modulesByType = modules.ToDictionary(module => module.Module.GetType());
        var ordered = new List<ModuleTestRecord>();
        var visitStates = new Dictionary<Type, ModuleVisitState>();
        var path = new Stack<Type>();

        foreach (var module in modules)
        {
            Visit(module, modulesByType, visitStates, ordered, path);
        }

        return ordered;
    }

    private static void Visit(
        ModuleTestRecord module,
        IReadOnlyDictionary<Type, ModuleTestRecord> modulesByType,
        IDictionary<Type, ModuleVisitState> visitStates,
        ICollection<ModuleTestRecord> ordered,
        Stack<Type> path)
    {
        var moduleType = module.Module.GetType();

        if (visitStates.TryGetValue(moduleType, out var state))
        {
            if (state == ModuleVisitState.Visited)
            {
                return;
            }

            var cyclePath = path
                .Reverse()
                .SkipWhile(type => type != moduleType)
                .Append(moduleType)
                .Select(type => type.FullName);

            throw new InvalidOperationException(
                $"Module dependency graph contains a cycle: {string.Join(" -> ", cyclePath)}.");
        }

        visitStates.Add(moduleType, ModuleVisitState.Visiting);
        path.Push(moduleType);

        foreach (var dependency in GetDependencies(moduleType))
        {
            if (modulesByType.TryGetValue(dependency.ModuleType, out var dependencyModule))
            {
                Visit(dependencyModule, modulesByType, visitStates, ordered, path);
                continue;
            }

            if (!dependency.Optional)
            {
                throw new InvalidOperationException(
                    $"Module '{moduleType.FullName}' depends on missing module '{dependency.ModuleType.FullName}'.");
            }
        }

        visitStates[moduleType] = ModuleVisitState.Visited;
        path.Pop();
        ordered.Add(module);
    }

    private static IEnumerable<DependsOnAttribute> GetDependencies(Type moduleType)
    {
        return moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .OfType<DependsOnAttribute>();
    }

    private enum ModuleVisitState
    {
        Visiting,
        Visited,
    }
}
