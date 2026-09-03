namespace AtomUI.City.Core.Lifecycle;

/// <summary>
/// Represents struct.
/// </summary>
public readonly record struct LifecycleStage
{
    private readonly string? _name;

    /// <summary>
    /// Initializes a new instance of the lifecycle stage class.
    /// </summary>
    public LifecycleStage(LifecycleStageArea area, string name)
    {
        if (!Enum.IsDefined(area))
        {
            throw new ArgumentOutOfRangeException(
                nameof(area),
                area,
                "Lifecycle stage area must be a defined value.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Area = area;
        _name = name;
    }

    /// <summary>
    /// Gets the area value.
    /// </summary>
    public LifecycleStageArea Area { get; }

    /// <summary>
    /// Gets the area name value.
    /// </summary>
    public string AreaName => Area.ToString();

    /// <summary>
    /// Gets the name value.
    /// </summary>
    public string Name => _name
        ?? throw new InvalidOperationException("The default lifecycle stage does not have a name.");

    /// <summary>
    /// Gets the key value.
    /// </summary>
    public string Key => AreaName + "." + Name;

    internal void ThrowIfInvalid(string parameterName)
    {
        if (!Enum.IsDefined(Area))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                Area,
                "Lifecycle stage area must be a defined value.");
        }

        if (string.IsNullOrWhiteSpace(_name))
        {
            throw new ArgumentException(
                "Lifecycle stage name cannot be null, empty, or whitespace.",
                parameterName);
        }
    }
}
