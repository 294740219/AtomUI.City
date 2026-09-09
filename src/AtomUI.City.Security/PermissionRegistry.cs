using System.Diagnostics.CodeAnalysis;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

public sealed class PermissionRegistry : IPermissionRegistry
{
    private readonly Dictionary<string, PermissionDescriptor> _permissions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _revokedContributions = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();
    private readonly OrderedEventPublisher<PermissionRegistryChangedEventArgs> _eventPublisher;
    private readonly IHostDiagnostics? _diagnostics;
    private long _revision;

    public PermissionRegistry()
    {
        _eventPublisher = new OrderedEventPublisher<PermissionRegistryChangedEventArgs>(
            diagnostics: null,
            SecurityDiagnosticIds.PermissionObserverFailed);
    }

    public PermissionRegistry(IHostDiagnostics diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _eventPublisher = new OrderedEventPublisher<PermissionRegistryChangedEventArgs>(
            diagnostics,
            SecurityDiagnosticIds.PermissionObserverFailed);
    }

    public event EventHandler<PermissionRegistryChangedEventArgs>? Changed;

    public long Revision
    {
        get
        {
            lock (_syncRoot)
            {
                return _revision;
            }
        }
    }

    public IReadOnlyCollection<PermissionDescriptor> Permissions
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_permissions.Values.ToArray());
            }
        }
    }

    public bool Add(PermissionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        long revision;
        PermissionRegistryChangedEventArgs args;
        bool shouldDrain;

        lock (_syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(descriptor.ContributionId)
                && _revokedContributions.Contains(descriptor.ContributionId))
            {
                return false;
            }

            if (_permissions.ContainsKey(descriptor.Name))
            {
                return false;
            }

            _permissions.Add(descriptor.Name, descriptor);
            revision = ++_revision;
            args = new PermissionRegistryChangedEventArgs(
                revision,
                descriptor.Name,
                descriptor.ContributionId);
            shouldDrain = _eventPublisher.Enqueue(Changed, args);
        }

        WriteChangedDiagnostic("Add", revision, descriptor.Name, descriptor.ContributionId);
        if (shouldDrain)
        {
            _eventPublisher.Drain(this);
        }

        return true;
    }

    public bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        long revision;
        PermissionDescriptor removed;
        PermissionRegistryChangedEventArgs args;
        bool shouldDrain;

        lock (_syncRoot)
        {
            if (!_permissions.Remove(name, out var descriptor))
            {
                return false;
            }

            removed = descriptor;
            revision = ++_revision;
            args = new PermissionRegistryChangedEventArgs(
                revision,
                removed.Name,
                removed.ContributionId);
            shouldDrain = _eventPublisher.Enqueue(Changed, args);
        }

        WriteChangedDiagnostic("Remove", revision, removed.Name, removed.ContributionId);
        if (shouldDrain)
        {
            _eventPublisher.Drain(this);
        }

        return true;
    }

    public int RemoveByContribution(string contributionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        long revision;
        int removedCount;
        PermissionRegistryChangedEventArgs args;
        bool shouldDrain;

        lock (_syncRoot)
        {
            _revokedContributions.Add(contributionId);
            var names = _permissions
                .Where(pair => string.Equals(pair.Value.ContributionId, contributionId, StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var name in names)
            {
                _permissions.Remove(name);
            }

            if (names.Length > 0)
            {
                revision = ++_revision;
            }
            else
            {
                return 0;
            }

            removedCount = names.Length;
            args = new PermissionRegistryChangedEventArgs(
                revision,
                permissionName: null,
                contributionId);
            shouldDrain = _eventPublisher.Enqueue(Changed, args);
        }

        WriteChangedDiagnostic("RevokeContribution", revision, permissionName: null, contributionId);
        if (shouldDrain)
        {
            _eventPublisher.Drain(this);
        }

        return removedCount;
    }

    public bool Contains(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_syncRoot)
        {
            return _permissions.ContainsKey(name);
        }
    }

    public bool TryGet(
        string name,
        [NotNullWhen(true)] out PermissionDescriptor? descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_syncRoot)
        {
            return _permissions.TryGetValue(name, out descriptor);
        }
    }

    private void WriteChangedDiagnostic(
        string operation,
        long revision,
        string? permissionName,
        string? contributionId)
    {
        SecurityDiagnostics.Write(
            _diagnostics,
            SecurityDiagnosticIds.PermissionRegistryChanged,
            "Permission registry changed.",
            HostDiagnosticSeverity.Info,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["operation"] = operation,
                ["permissionName"] = permissionName,
                ["contributionId"] = contributionId,
                ["revision"] = revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
    }
}
