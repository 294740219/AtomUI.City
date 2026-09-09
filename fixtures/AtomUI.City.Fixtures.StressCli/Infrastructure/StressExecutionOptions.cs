namespace AtomUI.City.Fixtures.StressCli.Infrastructure;

public enum StressProfile
{
    Quick,
    Standard,
    Extreme,
}

public sealed record StressExecutionOptions(
    StressProfile Profile,
    int Seed,
    int SoakIterations,
    int Operations,
    int Workers,
    int HostCycles,
    int RaceIterations,
    int DataIterations,
    TimeSpan PhaseTimeout)
{
    public static bool TryParse(
        IReadOnlyList<string> arguments,
        StressProfile defaultProfile,
        out StressExecutionOptions options,
        out string? error)
    {
        var profile = defaultProfile;
        int? seed = null;
        int? operations = null;
        int? workers = null;
        int? timeoutSeconds = null;
        int? dataIterations = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!TryReadOption(arguments, ref index, argument, out var name, out var value, out error))
            {
                options = Create(defaultProfile, 20260908);
                return false;
            }

            switch (name)
            {
                case "profile":
                    if (!Enum.TryParse<StressProfile>(value, ignoreCase: true, out profile))
                    {
                        options = Create(defaultProfile, 20260908);
                        error = $"未知 profile '{value}'，可选 quick、standard、extreme。";
                        return false;
                    }

                    break;
                case "seed":
                    if (!int.TryParse(value, out var parsedSeed))
                    {
                        options = Create(defaultProfile, 20260908);
                        error = $"seed 必须是 Int32，实际为 '{value}'。";
                        return false;
                    }

                    seed = parsedSeed;
                    break;
                case "operations":
                    if (!TryParsePositive(name, value, out var parsedOperations, out error))
                    {
                        options = Create(defaultProfile, 20260908);
                        return false;
                    }

                    operations = parsedOperations;
                    break;
                case "workers":
                    if (!TryParsePositive(name, value, out var parsedWorkers, out error))
                    {
                        options = Create(defaultProfile, 20260908);
                        return false;
                    }

                    workers = parsedWorkers;
                    break;
                case "timeout":
                    if (!TryParsePositive(name, value, out var parsedTimeout, out error))
                    {
                        options = Create(defaultProfile, 20260908);
                        return false;
                    }

                    timeoutSeconds = parsedTimeout;
                    break;
                case "data-iterations":
                    if (!TryParsePositive(name, value, out var parsedDataIterations, out error))
                    {
                        options = Create(defaultProfile, 20260908);
                        return false;
                    }

                    dataIterations = parsedDataIterations;
                    break;
                default:
                    options = Create(defaultProfile, 20260908);
                    error = $"未知参数 '--{name}'。";
                    return false;
            }
        }

        options = Create(profile, seed ?? 20260908) with
        {
            Operations = operations ?? Create(profile, 0).Operations,
            Workers = workers ?? Create(profile, 0).Workers,
            DataIterations = dataIterations ?? Create(profile, 0).DataIterations,
            PhaseTimeout = timeoutSeconds is null
                ? Create(profile, 0).PhaseTimeout
                : TimeSpan.FromSeconds(timeoutSeconds.Value),
        };
        error = null;
        return true;
    }

    public static StressExecutionOptions Create(StressProfile profile, int seed)
    {
        return profile switch
        {
            StressProfile.Quick => new StressExecutionOptions(
                profile, seed, 100, 5_000, 8, 2, 25, 100, TimeSpan.FromMinutes(1)),
            StressProfile.Standard => new StressExecutionOptions(
                profile, seed, 1_000, 50_000, 32, 10, 100, 1_000, TimeSpan.FromMinutes(5)),
            StressProfile.Extreme => new StressExecutionOptions(
                profile, seed, 5_000, 100_000, 64, 30, 250, 5_000, TimeSpan.FromMinutes(15)),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null),
        };
    }

    private static bool TryReadOption(
        IReadOnlyList<string> arguments,
        ref int index,
        string argument,
        out string name,
        out string value,
        out string? error)
    {
        name = string.Empty;
        value = string.Empty;
        error = null;
        if (!argument.StartsWith("--", StringComparison.Ordinal))
        {
            error = $"无法识别参数 '{argument}'。";
            return false;
        }

        var separator = argument.IndexOf('=');
        if (separator >= 0)
        {
            name = argument[2..separator].ToLowerInvariant();
            value = argument[(separator + 1)..];
        }
        else
        {
            name = argument[2..].ToLowerInvariant();
            if (index + 1 >= arguments.Count)
            {
                error = $"参数 '--{name}' 缺少值。";
                return false;
            }

            value = arguments[++index];
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
        {
            error = $"参数 '{argument}' 格式无效。";
            return false;
        }

        return true;
    }

    private static bool TryParsePositive(string name, string value, out int parsed, out string? error)
    {
        if (!int.TryParse(value, out parsed) || parsed <= 0)
        {
            error = $"{name} 必须是正整数，实际为 '{value}'。";
            return false;
        }

        error = null;
        return true;
    }
}
