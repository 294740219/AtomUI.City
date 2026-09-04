using AtomUI.City.Core.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TodoCli;

/// <summary>
/// Runs the CLI command once when the host starts, then requests shutdown.
/// </summary>
public sealed class CliRunner : IHostedService
{
    private readonly string[] _args;
    private readonly ITodoStore _store;
    private readonly ITodoFormatter _formatter;
    private readonly IOptions<TodoOptions> _options;
    private readonly IHostDiagnostics _diagnostics;
    private readonly ILogger<CliRunner> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly TraceSink _trace;

    public CliRunner(
        string[] args,
        ITodoStore store,
        ITodoFormatter formatter,
        IOptions<TodoOptions> options,
        IHostDiagnostics diagnostics,
        ILogger<CliRunner> logger,
        IHostApplicationLifetime lifetime,
        TraceSink trace)
    {
        _args = args;
        _store = store;
        _formatter = formatter;
        _options = options;
        _diagnostics = diagnostics;
        _logger = logger;
        _lifetime = lifetime;
        _trace = trace;
    }

    public int ExitCode { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            ExitCode = Run();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Command failed.");
            _diagnostics.Write(new HostDiagnosticRecord(
                "TODO001",
                $"Command failed: {exception.Message}",
                HostDiagnosticSeverity.Error));
            ExitCode = 1;
        }

        // Signal the host to stop once the command has run.
        _lifetime.StopApplication();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private int Run()
    {
        var verb = _args.FirstOrDefault();

        switch (verb)
        {
            case "add":
                return RunAdd();
            case "list":
                return RunList();
            case "complete":
                return RunComplete();
            case "--help":
            case "-h":
                return RunHelp();
            default:
                Console.Error.WriteLine("Usage: TodoCli <add|list|complete|--help>");
                return 2;
        }
    }

    private int RunAdd()
    {
        var title = _args.Skip(1).FirstOrDefault()
                    ?? _options.Value.DefaultTitle
                    ?? "untitled";

        if (string.IsNullOrWhiteSpace(title))
        {
            Console.Error.WriteLine("Title cannot be empty.");
            return 2;
        }

        var item = _store.Add(title);
        _diagnostics.Write(new HostDiagnosticRecord(
            "TODO002",
            $"Added todo #{item.Id}.",
            HostDiagnosticSeverity.Info));
        _logger.LogInformation("Added todo #{Id}: {Title}", item.Id, item.Title);
        Console.WriteLine(_formatter.Format(item));
        return 0;
    }

    private int RunList()
    {
        foreach (var item in _store.List())
        {
            var line = _formatter.Format(item);
            _trace.Write(line);
            Console.WriteLine(line);
        }

        return 0;
    }

    private int RunComplete()
    {
        if (!int.TryParse(_args.Skip(1).FirstOrDefault(), out var id))
        {
            Console.Error.WriteLine("A numeric id is required.");
            return 2;
        }

        return _store.Complete(id)
            ? 0
            : 3;
    }

    private static int RunHelp()
    {
        Console.WriteLine("TodoCli <command>");
        Console.WriteLine("  add <title>    Add a todo item.");
        Console.WriteLine("  list           List todo items.");
        Console.WriteLine("  complete <id>  Mark a todo item done.");
        return 0;
    }
}
