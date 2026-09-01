using AtomUI.City.Core.Modularity;

namespace AtomUI.City.Core.HeadlessApp;

internal static class GeneratedHeadlessRecorder
{
    private static readonly object SyncRoot = new();
    private static readonly List<string> Entries = [];

    public static IReadOnlyList<string> Calls
    {
        get
        {
            lock (SyncRoot)
            {
                return Entries.ToArray();
            }
        }
    }

    public static void Record(string value)
    {
        lock (SyncRoot)
        {
            Entries.Add(value);
        }
    }

    public static void Reset()
    {
        lock (SyncRoot)
        {
            Entries.Clear();
        }

        GeneratedUnusedModule.CreatedCount = 0;
    }
}

public sealed class GeneratedFoundationModule : ModuleBase
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GeneratedHeadlessRecorder.Record("generated:foundation:configure");
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        GeneratedHeadlessRecorder.Record("generated:foundation:initialize");
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        GeneratedHeadlessRecorder.Record("generated:foundation:shutdown");
    }
}

[ApplicationModule]
[DependsOn(typeof(GeneratedFoundationModule))]
public sealed class GeneratedApplicationModule : ModuleBase
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GeneratedHeadlessRecorder.Record("generated:application:configure");
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        GeneratedHeadlessRecorder.Record("generated:application:initialize");
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        GeneratedHeadlessRecorder.Record("generated:application:shutdown");
    }
}

public sealed class GeneratedUnusedModule : ModuleBase
{
    public GeneratedUnusedModule()
    {
        CreatedCount++;
    }

    public static int CreatedCount { get; set; }
}
