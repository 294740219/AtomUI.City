using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents module context.
/// </summary>
public sealed class ModuleContext
{
    /// <summary>
    /// Initializes a new instance of the module context class.
    /// </summary>
    public ModuleContext(string name, IApplicationContext applicationContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(applicationContext);

        Name = name;
        ApplicationContext = applicationContext;
    }

    /// <summary>
    /// Gets the name value.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the application context value.
    /// </summary>
    public IApplicationContext ApplicationContext { get; }
}
