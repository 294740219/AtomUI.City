using System.Collections.Concurrent;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using AtomUI.City.Core.Modularity;
using AtomUI.City.CoreEventBus.DogfoodApp.Contracts;
using AtomUI.City.CoreEventBus.DogfoodApp.Services;
using AtomUI.City.CoreEventBus.DogfoodApp.Verification;
using AtomUI.City.EventBus;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.CoreEventBus.DogfoodApp;

internal static class DogfoodScenarioRunner
{
    private const int ExpectedModuleCount = 10;
    private const int ExpectedServiceCount = 31;
    private const int ExpectedContractCount = 12;
    private const int ExpectedHandlerCount = 20;
    private static readonly string[] SupportedScenarios =
        ["happy-path", "ordering", "concurrent", "failure", "ownership", "cancel-stop"];

    public static async Task<DogfoodRunResult> RunAsync(IApplicationHost host, string scenario)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        ValidateProductShape(host);
        var executed = 0;
        var stopped = false;

        if (string.Equals(scenario, "verify-all", StringComparison.Ordinal))
        {
            foreach (var item in SupportedScenarios)
            {
                stopped = await RunOneAsync(host, item);
                executed++;
            }
        }
        else
        {
            if (!SupportedScenarios.Contains(scenario, StringComparer.Ordinal))
            {
                throw new ArgumentException($"Unknown scenario '{scenario}'.", nameof(scenario));
            }

            stopped = await RunOneAsync(host, scenario);
            executed = 1;
        }

        return new DogfoodRunResult(
            "passed",
            ExpectedModuleCount,
            ExpectedServiceCount,
            ExpectedContractCount,
            ExpectedHandlerCount,
            executed,
            stopped);
    }

    private static Task<bool> RunOneAsync(IApplicationHost host, string scenario) => scenario switch
    {
        "happy-path" => RunAndContinueAsync(() => VerifyHappyPathAsync(host)),
        "ordering" => RunAndContinueAsync(() => VerifyOrderingAsync(host)),
        "concurrent" => RunAndContinueAsync(() => VerifyConcurrentPartitionsAsync(host)),
        "failure" => RunAndContinueAsync(() => VerifyFailurePoliciesAsync(host)),
        "ownership" => RunAndContinueAsync(() => VerifyOwnershipAsync(host)),
        "cancel-stop" => VerifyConcurrentStopAsync(host),
        _ => throw new InvalidOperationException($"Unsupported scenario '{scenario}'.")
    };

    private static async Task<bool> RunAndContinueAsync(Func<Task> operation)
    {
        await operation();
        return false;
    }

    private static void ValidateProductShape(IApplicationHost host)
    {
        var modules = host.Services.GetRequiredService<IModuleRegistry>().Modules;
        Ensure(modules.Count == ExpectedModuleCount,
            $"Expected {ExpectedModuleCount} selected modules, but found {modules.Count}.");

        var contracts = host.Services.GetRequiredService<IEventContractRegistry>().Descriptors;
        Ensure(contracts.Count == ExpectedContractCount,
            $"Expected {ExpectedContractCount} event contracts, but found {contracts.Count}.");

        using var firstScope = host.Services.CreateScope();
        using var secondScope = host.Services.CreateScope();
        var first = firstScope.ServiceProvider;
        var second = secondScope.ServiceProvider;

        object[] services =
        [
            first.GetRequiredService<ISystemClock>(), first.GetRequiredService<IIdentifierFactory>(),
            first.GetRequiredService<IIdentityValidator>(), first.GetRequiredService<IWorkspaceCatalog>(),
            first.GetRequiredService<IJobRepository>(), first.GetRequiredService<IJobPlanner>(),
            first.GetRequiredService<IExecutionEngine>(), first.GetRequiredService<IAuditSink>(),
            first.GetRequiredService<IReportBuilder>(), first.GetRequiredService<INotificationGateway>(),
            first.GetRequiredService<IUserSession>(), first.GetRequiredService<IWorkspaceSession>(),
            first.GetRequiredService<IJobSession>(), first.GetRequiredService<IExecutionSession>(),
            first.GetRequiredService<IAuditSession>(), first.GetRequiredService<IReportSession>(),
            first.GetRequiredService<INotificationSession>(), first.GetRequiredService<IMaintenanceSession>(),
            first.GetRequiredService<ICorrelationSession>(), first.GetRequiredService<IUnitOfWork>(),
            first.GetRequiredService<ISubmissionPolicy>(), first.GetRequiredService<IWorkspacePolicy>(),
            first.GetRequiredService<IJobValidator>(), first.GetRequiredService<IExecutionPolicy>(),
            first.GetRequiredService<IAuditFormatter>(), first.GetRequiredService<IReportFormatter>(),
            first.GetRequiredService<INotificationFormatter>(), first.GetRequiredService<IMaintenancePolicy>(),
            first.GetRequiredService<IRetryPolicy>(), first.GetRequiredService<IWorkflowCoordinator>(),
            first.GetRequiredService<IDogfoodLedger>()
        ];
        Ensure(services.Length == ExpectedServiceCount, "Generated service matrix is incomplete.");
        Ensure(ReferenceEquals(first.GetRequiredService<ISystemClock>(), second.GetRequiredService<ISystemClock>()),
            "Singleton service identity changed across scopes.");
        Ensure(ReferenceEquals(first.GetRequiredService<IUserSession>(), first.GetRequiredService<IUserSession>()),
            "Scoped service identity changed inside one scope.");
        Ensure(!ReferenceEquals(first.GetRequiredService<IUserSession>(), second.GetRequiredService<IUserSession>()),
            "Scoped service leaked across scopes.");
        Ensure(!ReferenceEquals(first.GetRequiredService<ISubmissionPolicy>(), first.GetRequiredService<ISubmissionPolicy>()),
            "Transient service was unexpectedly reused.");
        Ensure(first.GetRequiredService<IWorkflowCoordinator>().Prepare("workspace-main", 1).Length > 0,
            "The multi-level DI dependency chain did not execute.");
    }

    private static async Task VerifyHappyPathAsync(IApplicationHost host)
    {
        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        var ledger = host.Services.GetRequiredService<IDogfoodLedger>();
        var repository = host.Services.GetRequiredService<IJobRepository>();
        ledger.Reset();
        const string workspace = "workspace-happy";
        const string job = "job-happy";
        repository.Store(job);

        var results = new[]
        {
            await publisher.PublishAsync(new UserAuthenticated("dogfood-user")),
            await publisher.PublishAsync(new WorkspaceOpened(workspace, "dogfood-user")),
            await publisher.PublishAsync(new EventChannel<JobSubmitted>("commands"), new JobSubmitted(workspace, job, 1)),
            await publisher.PublishAsync(new JobAccepted(workspace, job)),
            await publisher.PublishAsync(new JobQueued(workspace, job)),
            await publisher.PublishAsync(new ExecutionStarted(workspace, job)),
            await publisher.PublishAsync(new EventChannel<JobProgressed>("jobs"), new JobProgressed(workspace, job, 1),
                new EventPublishOptions { PartitionKey = workspace, CorrelationId = "happy-path" }),
            await publisher.PublishAsync(new JobCompleted(workspace, job, true)),
            await publisher.PublishAsync(new AuditRecorded(job, "completed")),
            await publisher.PublishAsync(new ReportGenerated(job, "report-happy")),
            await publisher.PublishAsync(new EventChannel<NotificationSent>("telemetry"), new NotificationSent(job, "console")),
            await publisher.PublishAsync(new EventChannel<MaintenanceCycle>("failures"), new MaintenanceCycle("cycle-happy", 1))
        };

        Ensure(results.All(static result => result.Succeeded), "The happy-path workflow contained a failed delivery.");
        Ensure(results.Sum(static result => result.Deliveries.Count) == ExpectedHandlerCount,
            "The happy-path workflow did not reach all generated handlers.");
        Ensure(ledger.Total == ExpectedHandlerCount && ledger.Snapshot().Count == ExpectedHandlerCount,
            "Generated handler execution was lost or duplicated.");
    }

    private static async Task VerifyOrderingAsync(IApplicationHost host)
    {
        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        var subscriber = host.Services.GetRequiredService<IEventSubscriber>();
        var channel = new EventChannel<JobSubmitted>("commands");
        await using var owner = host.ApplicationScope!.CreateChild(LifecycleScopeKind.Operation, "dogfood-ordering");
        var observed = new List<int>();
        subscriber.Subscribe(owner, channel, context =>
        {
            lock (observed) { observed.Add(context.Event.Sequence); }
            return ValueTask.CompletedTask;
        });

        var post = await publisher.PostAsync(channel, new JobSubmitted("workspace-order", "job-1", 1));
        var publish = await publisher.PublishAsync(channel, new JobSubmitted("workspace-order", "job-2", 2));
        Ensure(post.Accepted && publish.Succeeded, "Mixed Post/Publish admission failed.");
        lock (observed) { Ensure(observed.SequenceEqual([1, 2]), "Shared admission order was not preserved."); }
    }

    private static async Task VerifyConcurrentPartitionsAsync(IApplicationHost host)
    {
        const int workspaceCount = 8;
        const int jobsPerWorkspace = 25;
        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        var subscriber = host.Services.GetRequiredService<IEventSubscriber>();
        var channel = new EventChannel<JobProgressed>("jobs");
        await using var owner = host.ApplicationScope!.CreateChild(LifecycleScopeKind.Operation, "dogfood-concurrent");
        var observed = new ConcurrentDictionary<string, ConcurrentQueue<int>>(StringComparer.Ordinal);
        subscriber.Subscribe(owner, channel, context =>
        {
            observed.GetOrAdd(context.Event.WorkspaceId, static _ => new ConcurrentQueue<int>()).Enqueue(context.Event.Sequence);
            return ValueTask.CompletedTask;
        });

        var publications = new List<Task<EventPublishResult>>(workspaceCount * jobsPerWorkspace);
        for (var sequence = 0; sequence < jobsPerWorkspace; sequence++)
        {
            for (var workspaceIndex = 0; workspaceIndex < workspaceCount; workspaceIndex++)
            {
                var workspace = $"workspace-{workspaceIndex}";
                publications.Add(publisher.PublishAsync(
                    channel,
                    new JobProgressed(workspace, $"{workspace}-job-{sequence}", sequence),
                    new EventPublishOptions { PartitionKey = workspace, CorrelationId = $"batch-{workspaceIndex}" }).AsTask());
            }
        }

        var results = await Task.WhenAll(publications);
        Ensure(results.Length == workspaceCount * jobsPerWorkspace && results.All(static result => result.Succeeded),
            "Concurrent partition publication lost or failed work.");
        Ensure(results.Sum(static result => result.Deliveries.Count) == workspaceCount * jobsPerWorkspace * 3,
            "Concurrent partition delivery count is not conserved.");
        Ensure(observed.Count == workspaceCount, "One or more partitions were not observed.");
        foreach (var queue in observed.Values)
        {
            Ensure(queue.SequenceEqual(Enumerable.Range(0, jobsPerWorkspace)), "Per-partition order was not preserved.");
        }
    }

    private static async Task VerifyFailurePoliciesAsync(IApplicationHost host)
    {
        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        var subscriber = host.Services.GetRequiredService<IEventSubscriber>();
        var channel = new EventChannel<MaintenanceCycle>("failures");
        await using var owner = host.ApplicationScope!.CreateChild(LifecycleScopeKind.Operation, "dogfood-failure");
        var laterCalled = false;
        subscriber.Subscribe<MaintenanceCycle>(owner, channel, _ => throw new InvalidOperationException("expected-dogfood-failure"),
            EventSubscriptionOptions.Serialized.WithErrorPolicy(EventErrorPolicy.StopPublication));
        subscriber.Subscribe(owner, channel, _ => { laterCalled = true; return ValueTask.CompletedTask; });
        var result = await publisher.PublishAsync(channel, new MaintenanceCycle("cycle-failure", 0));
        Ensure(result.FailedCount == 1 && result.SkippedCount >= 1 && !laterCalled,
            "StopPublication did not expose failure and skip later delivery.");
        await owner.StopAsync();

        await using var disableOwner = host.ApplicationScope.CreateChild(LifecycleScopeKind.Operation, "dogfood-disable");
        var attempts = 0;
        subscriber.Subscribe<MaintenanceCycle>(disableOwner, channel, _ =>
        {
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("expected-disable-failure");
        }, EventSubscriptionOptions.Serialized
            .WithErrorPolicy(EventErrorPolicy.DisableSubscription)
            .WithDisableSubscriptionAfterFailures(2));
        await publisher.PublishAsync(channel, new MaintenanceCycle("cycle-disable-1", 0));
        await publisher.PublishAsync(channel, new MaintenanceCycle("cycle-disable-2", 0));
        await publisher.PublishAsync(channel, new MaintenanceCycle("cycle-disable-3", 0));
        Ensure(attempts == 2, "DisableSubscription did not stop the repeatedly failing handler.");
    }

    private static async Task VerifyOwnershipAsync(IApplicationHost host)
    {
        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        var subscriber = host.Services.GetRequiredService<IEventSubscriber>();
        var channel = new EventChannel<NotificationSent>("telemetry");
        await using var owner = host.ApplicationScope!.CreateChild(LifecycleScopeKind.Operation, "dogfood-owner-a");
        await using var otherOwner = host.ApplicationScope.CreateChild(LifecycleScopeKind.Operation, "dogfood-owner-b");
        var first = 0;
        var second = 0;
        subscriber.Subscribe(owner, channel, _ => { Interlocked.Increment(ref first); return ValueTask.CompletedTask; });
        subscriber.Subscribe(otherOwner, channel, _ => { Interlocked.Increment(ref second); return ValueTask.CompletedTask; });
        await publisher.PublishAsync(channel, new NotificationSent("job-owner-1", "console"));
        await owner.StopAsync();
        await publisher.PublishAsync(channel, new NotificationSent("job-owner-2", "console"));
        Ensure(first == 1 && second == 2, "Stopping one owner affected the wrong subscription line.");
    }

    private static async Task<bool> VerifyConcurrentStopAsync(IApplicationHost host)
    {
        var publisher = host.Services.GetRequiredService<IEventPublisher>();
        var channel = new EventChannel<NotificationSent>("telemetry");
        for (var i = 0; i < 50; i++)
        {
            var post = await publisher.PostAsync(channel, new NotificationSent($"job-stop-{i}", "console"));
            Ensure(post.Accepted, "Pre-stop work was unexpectedly rejected.");
        }

        await Task.WhenAll(host.StopAsync(), host.StopAsync());
        var rejected = false;
        try
        {
            await publisher.PublishAsync(channel, new NotificationSent("job-after-stop", "console"));
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Ensure(rejected, "The stopped EventBus accepted a new publication.");
        Ensure(host.HostScope.State == LifecycleScopeState.Stopped, "Host scope did not reach Stopped.");
        return true;
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}

internal sealed record DogfoodRunResult(
    string Status,
    int Modules,
    int Services,
    int Contracts,
    int Handlers,
    int Scenarios,
    bool HostStopped)
{
    public string ToJson() =>
        $"{{\"status\":\"{Status}\",\"modules\":{Modules},\"services\":{Services},\"contracts\":{Contracts},\"handlers\":{Handlers},\"scenarios\":{Scenarios}}}";
}
