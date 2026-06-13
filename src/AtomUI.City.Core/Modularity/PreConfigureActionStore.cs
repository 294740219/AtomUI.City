namespace AtomUI.City.Modularity;

internal sealed class PreConfigureActionStore
{
    private readonly Dictionary<Type, List<Action<object>>> _actionsByOptionsType = [];

    public void Add<TOptions>(Action<TOptions> action)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(action);

        var optionsType = typeof(TOptions);

        if (!_actionsByOptionsType.TryGetValue(optionsType, out var actions))
        {
            actions = [];
            _actionsByOptionsType.Add(optionsType, actions);
        }

        actions.Add(options => action((TOptions)options));
    }

    public void Apply<TOptions>(TOptions options)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!_actionsByOptionsType.TryGetValue(typeof(TOptions), out var actions))
        {
            return;
        }

        foreach (var action in actions.ToArray())
        {
            action(options);
        }
    }
}
