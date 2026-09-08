using System.Collections.Frozen;

namespace AtomUI.City.State;

public sealed class StateWriteAuthority
{
    private readonly FrozenSet<string> _capabilities;

    private StateWriteAuthority(
        StateWriteAuthorityKind kind,
        string? moduleName,
        string? pluginId,
        IEnumerable<string>? capabilities)
    {
        Kind = kind;
        ModuleName = moduleName;
        PluginId = pluginId;
        _capabilities = (capabilities ?? []).ToFrozenSet(StringComparer.Ordinal);

        if (_capabilities.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Write capabilities must not contain null or whitespace values.", nameof(capabilities));
        }
    }

    public StateWriteAuthorityKind Kind { get; }

    public string? ModuleName { get; }

    public string? PluginId { get; }

    public IReadOnlySet<string> Capabilities => _capabilities;

    internal static StateWriteAuthority Host(IEnumerable<string>? capabilities = null)
    {
        return new StateWriteAuthority(StateWriteAuthorityKind.Host, null, null, capabilities);
    }

    public static StateWriteAuthority Module(
        string moduleName,
        IEnumerable<string>? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        return new StateWriteAuthority(StateWriteAuthorityKind.Module, moduleName, null, capabilities);
    }

    public static StateWriteAuthority Plugin(
        string pluginId,
        IEnumerable<string>? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        return new StateWriteAuthority(StateWriteAuthorityKind.Plugin, null, pluginId, capabilities);
    }

    internal bool HasCapability(string capability)
    {
        return _capabilities.Contains(capability);
    }
}
