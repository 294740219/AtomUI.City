namespace AtomUI.City.Testing;

public sealed class PluginTestRecord
{
    private readonly List<string> _contributions = [];

    public PluginTestRecord(string id, string version, string installPath, PluginTestState state)
    {
        Id = id;
        Version = version;
        InstallPath = installPath;
        State = state;
    }

    public string Id { get; }

    public string Version { get; }

    public string InstallPath { get; }

    public PluginTestState State { get; internal set; }

    public IReadOnlyCollection<string> Contributions => Array.AsReadOnly(_contributions.ToArray());

    public int RevokedContributionCount { get; private set; }

    internal void AddContribution(string contributionId)
    {
        if (!_contributions.Contains(contributionId, StringComparer.Ordinal))
        {
            _contributions.Add(contributionId);
        }
    }

    internal int RevokeContributions()
    {
        var revokedCount = _contributions.Count;

        if (revokedCount == 0)
        {
            return 0;
        }

        _contributions.Clear();
        RevokedContributionCount += revokedCount;

        return revokedCount;
    }
}
