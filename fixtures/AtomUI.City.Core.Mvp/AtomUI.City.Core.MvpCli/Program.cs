namespace AtomUI.City.Core.MvpCli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var result = await MvpVerifier.RunAsync(args).ConfigureAwait(false);
        Console.WriteLine(MvpJsonSerializer.Serialize(result));
        return result.Success ? 0 : result.ExitCode;
    }
}
