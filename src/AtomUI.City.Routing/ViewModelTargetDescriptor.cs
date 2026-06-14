namespace AtomUI.City.Routing;

public sealed class ViewModelTargetDescriptor
{
    public ViewModelTargetDescriptor(Type viewModelType)
        : this(viewModelType, parameterBindings: null, reuseKey: null, activationHint: null)
    {
    }

    public ViewModelTargetDescriptor(
        Type viewModelType,
        IReadOnlyList<string>? parameterBindings,
        string? reuseKey = null,
        string? activationHint = null)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        ViewModelType = viewModelType;
        ParameterBindings = AsReadOnly(parameterBindings);
        ReuseKey = string.IsNullOrWhiteSpace(reuseKey) ? null : reuseKey;
        ActivationHint = string.IsNullOrWhiteSpace(activationHint) ? null : activationHint;
    }

    public Type ViewModelType { get; }

    public IReadOnlyList<string> ParameterBindings { get; }

    public string? ReuseKey { get; }

    public string? ActivationHint { get; }

    private static IReadOnlyList<string> AsReadOnly(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        foreach (var value in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(values));
        }

        return Array.AsReadOnly(values.ToArray());
    }
}
