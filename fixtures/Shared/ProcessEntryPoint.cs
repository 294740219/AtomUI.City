using System.Runtime.InteropServices;

namespace AtomUI.City.Fixtures;

internal static class ProcessEntryPoint
{
    private const uint SemNoGpFaultErrorBox = 0x0002;

    public static async Task<int> RunAsync(Func<Task<int>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        SuppressWindowsErrorDialogs();

        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void SuppressWindowsErrorDialogs()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var currentMode = GetErrorMode();
        SetErrorMode(currentMode | SemNoGpFaultErrorBox);
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetErrorMode();

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint errorMode);
}
