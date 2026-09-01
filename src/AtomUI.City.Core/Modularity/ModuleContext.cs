using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.Modularity;

public sealed class ModuleContext
{
    public ModuleContext(string name, IApplicationContext applicationContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(applicationContext);

        Name = name;
        ApplicationContext = applicationContext;
    }

    public string Name { get; }

    public IApplicationContext ApplicationContext { get; }
}
