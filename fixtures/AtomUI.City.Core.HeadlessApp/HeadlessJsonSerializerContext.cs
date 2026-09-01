using System.Text.Json.Serialization;

namespace AtomUI.City.Core.HeadlessApp;

internal sealed record GeneratedModulesScenarioResult(
    string Scenario,
    bool Success,
    string[] Calls,
    string[] LoadedModules,
    int UnusedCreatedCount);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GeneratedModulesScenarioResult))]
internal sealed partial class HeadlessJsonSerializerContext : JsonSerializerContext;
