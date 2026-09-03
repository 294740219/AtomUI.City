using System.Collections.ObjectModel;
using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Core.Diagnostics;

/// <summary>
/// Represents host diagnostic record.
/// </summary>
public sealed record HostDiagnosticRecord
{
    private string _code = null!;
    private IReadOnlyDictionary<string, string?> _context =
        new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>(StringComparer.Ordinal));
    private string _message = null!;
    private HostDiagnosticSeverity _severity;
    private LifecycleStage? _stage;

    /// <summary>
    /// Initializes a new instance of the host diagnostic record class.
    /// </summary>
    public HostDiagnosticRecord(
        string Code,
        string Message,
        HostDiagnosticSeverity Severity,
        string? ScopeId = null,
        LifecycleStage? Stage = null)
    {
        this.Code = Code;
        this.Message = Message;
        this.Severity = Severity;
        this.ScopeId = ScopeId;
        this.Stage = Stage;
    }

    /// <summary>
    /// Gets the code value.
    /// </summary>
    public string Code
    {
        get => _code;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _code = value;
        }
    }

    /// <summary>
    /// Gets the message value.
    /// </summary>
    public string Message
    {
        get => _message;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _message = value;
        }
    }

    /// <summary>
    /// Gets the severity value.
    /// </summary>
    public HostDiagnosticSeverity Severity
    {
        get => _severity;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Diagnostic severity must be a defined value.");
            }

            _severity = value;
        }
    }

    /// <summary>
    /// Gets or sets the scope id value.
    /// </summary>
    public string? ScopeId { get; init; }

    /// <summary>
    /// Gets or sets the stage value.
    /// </summary>
    public LifecycleStage? Stage
    {
        get => _stage;
        init
        {
            if (value is { } stage)
            {
                stage.ThrowIfInvalid(nameof(Stage));
            }

            _stage = value;
        }
    }

    /// <summary>
    /// Gets the context value.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Context
    {
        get => _context;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            _context = new ReadOnlyDictionary<string, string?>(
                new Dictionary<string, string?>(value, StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// Executes the deconstruct operation.
    /// </summary>
    public void Deconstruct(
        out string code,
        out string message,
        out HostDiagnosticSeverity severity,
        out string? scopeId,
        out LifecycleStage? stage)
    {
        code = Code;
        message = Message;
        severity = Severity;
        scopeId = ScopeId;
        stage = Stage;
    }
}
