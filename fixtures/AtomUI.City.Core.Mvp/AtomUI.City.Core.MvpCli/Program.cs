using AtomUI.City.Fixtures;

namespace AtomUI.City.Core.MvpCli;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        return ProcessEntryPoint.RunAsync(() => RunAsync(args));
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args is ["--test-entry-failure"])
        {
            throw new InvalidOperationException("Core MVP fixture entry failure.");
        }

        var result = await MvpVerifier.RunAsync(args).ConfigureAwait(false);
        Console.WriteLine(MvpJsonSerializer.Serialize(result));
        return result.Success ? 0 : result.ExitCode;
    }
}
