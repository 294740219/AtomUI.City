using AtomUI.City.Core.Modularity;

namespace AtomUI.City.Core.CyclicModules;

public static class CyclicModuleConstructionRecorder
{
    private static int _firstCount;
    private static int _secondCount;

    public static int FirstCount => Volatile.Read(ref _firstCount);

    public static int SecondCount => Volatile.Read(ref _secondCount);

    public static void RecordFirst() => Interlocked.Increment(ref _firstCount);

    public static void RecordSecond() => Interlocked.Increment(ref _secondCount);

    public static void Reset()
    {
        Volatile.Write(ref _firstCount, 0);
        Volatile.Write(ref _secondCount, 0);
    }
}

[DependsOn(typeof(SecondCyclicModule))]
public sealed class FirstCyclicModule : ModuleBase
{
    public FirstCyclicModule()
    {
        CyclicModuleConstructionRecorder.RecordFirst();
    }
}

[DependsOn(typeof(FirstCyclicModule))]
public sealed class SecondCyclicModule : ModuleBase
{
    public SecondCyclicModule()
    {
        CyclicModuleConstructionRecorder.RecordSecond();
    }
}
