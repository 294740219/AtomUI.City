using System.Runtime.CompilerServices;
using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.Modularity;

internal static class ModuleRegistrationStore
{
    private static readonly ConditionalWeakTable<
        IApplicationHostBuilder,
        FreezableApplicationHostBuilderCollection<ModuleRegistration>> Stores = new();

    public static void Add(IApplicationHostBuilder builder, ModuleRegistration registration)
    {
        Stores.GetOrCreateValue(builder).AddIfAbsent(
            registration,
            existing => existing.ModuleType == registration.ModuleType);
    }

    public static IReadOnlyList<ModuleRegistration> FreezeAndSnapshot(
        IApplicationHostBuilder builder)
    {
        return Stores.GetOrCreateValue(builder).FreezeAndSnapshot();
    }
}
