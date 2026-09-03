using System.Text.Json;
using System.Text.Json.Serialization;

namespace AtomUI.City.Core.MvpCli;

internal sealed record MvpVerificationResult(
    string Scenario,
    bool Success,
    int ExitCode,
    int SelectedModuleCount,
    int SelectedServiceCount,
    int PermutationCount,
    int CombinationCount,
    int ConcurrentScopeCount,
    string[] Failures);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MvpVerificationResult))]
internal sealed partial class MvpJsonSerializerContext : JsonSerializerContext;

internal static class MvpJsonSerializer
{
    public static string Serialize(MvpVerificationResult result) =>
        JsonSerializer.Serialize(result, MvpJsonSerializerContext.Default.MvpVerificationResult);
}
