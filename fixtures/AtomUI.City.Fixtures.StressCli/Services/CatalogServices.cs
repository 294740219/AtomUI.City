namespace AtomUI.City.Fixtures.StressCli.Services;

// DataCatalog(3)

public interface IDataCatalog
{
    int Revision { get; }
    void Rebuild();
}

public sealed class CatalogService : IDataCatalog
{
    private int _revision;
    public int Revision => _revision;
    public void Rebuild() => Interlocked.Increment(ref _revision);
}

public interface ICatalogIndex
{
    int Entries { get; }
    void Add(string entry);
}

public sealed class IndexService : ICatalogIndex
{
    private readonly List<string> _entries = [];
    public int Entries => _entries.Count;
    public void Add(string entry) => _entries.Add(entry);
}

public interface ICatalogValidator
{
    bool Validate(string candidate);
}

public sealed class CatalogValidator : ICatalogValidator
{
    public bool Validate(string candidate) => !string.IsNullOrWhiteSpace(candidate);
}

// Messaging(3)

public interface IMessageCodec
{
    string Encode(string payload);
}

public sealed class JsonMessageCodec : IMessageCodec
{
    public string Encode(string payload) => $"{{\"payload\":\"{payload}\"}}";
}

public interface IMessageJournal
{
    int Entries { get; }
    void Append(string entry);
}

public sealed class JournalService : IMessageJournal
{
    private readonly List<string> _entries = [];
    public int Entries => _entries.Count;
    public void Append(string entry) => _entries.Add(entry);
}

public interface IMessageDeduper
{
    bool Seen(string fingerprint);
}

public sealed class DeduperService : IMessageDeduper
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    public bool Seen(string fingerprint) => !_seen.Add(fingerprint);
}

// Security(1)

public interface ISecurityBoundary
{
    bool IsTrusted(string subject);
}

public sealed class SecurityBoundaryService : ISecurityBoundary
{
    public bool IsTrusted(string subject) => !string.IsNullOrWhiteSpace(subject);
}

// Messaging 补充（第 36 个服务）

public interface IMessageQueue
{
    int Queued { get; }
    void Enqueue(string message);
}

public sealed class MessageQueueService : IMessageQueue
{
    private int _queued;
    public int Queued => _queued;
    public void Enqueue(string message) => Interlocked.Increment(ref _queued);
}
