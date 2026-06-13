# Phase 0-1 Core Kernel 实施计划

> **给代理执行者：** 必须使用 `superpowers:subagent-driven-development`（推荐）或 `superpowers:executing-plans` 按任务执行本计划。步骤使用 checkbox（`- [ ]`）语法跟踪。

**目标：** 交付产品级 Phase 0 工程基线和 Phase 1 `AtomUI.City.Core` 最小内核，使实现、公开 API 合同、测试矩阵和工程门禁一致。

**架构：** 本计划只修改 `AtomUI.City.Core`、`AtomUI.City.Core.Tests` 和 Core 相关文档。实现顺序先冻结公开合同，再用测试驱动 Host、生命周期、模块、诊断、DI marker 和 UI dispatcher 的产品语义，最后通过完整 Core 门禁确认阶段完成。

**技术栈：** .NET `net10.0` Debug 目标、.NET `net8.0` Release 目标、`Microsoft.Extensions.Hosting`、`Microsoft.Extensions.DependencyInjection.Abstractions`、xUnit、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 范围决策

这是下一轮研发任务计划，只覆盖 `docs/engineering/implementation-roadmap.md` 中的 Phase 0 和 Phase 1。

包含：

- Phase 0 的 restore、build、test、包边界和文档门禁。
- Phase 1 Core Kernel 的 `AUC-CORE-001` 到 `AUC-CORE-007`。
- `docs/engineering/public-api-review.md` 要求的 Core public API method contract。
- Host、生命周期、模块、诊断、DI marker 和 dispatcher 的产品级 contract 测试。

不包含：

- Phase 2 的 `AtomUI.City.Testing` 测试工具包。
- Routing、Presentation、PluginSystem 动态加载、Source Generator、CLI、Templates 和 SalesDesk 纵切片。
- 未写入 `docs/modules/core/api-contracts.md` 的新增 public API。

## 文件地图

Core 实现文件：

- `src/AtomUI.City.Core/Hosting/ApplicationHost.cs`
- `src/AtomUI.City.Core/Hosting/ApplicationHostBuilder.cs`
- `src/AtomUI.City.Core/Hosting/IApplicationHostBuilder.cs`
- `src/AtomUI.City.Core/Hosting/DefaultApplicationHost.cs`
- `src/AtomUI.City.Core/Hosting/ApplicationContext.cs`
- `src/AtomUI.City.Core/Lifecycle/LifecyclePipeline.cs`
- `src/AtomUI.City.Core/Lifecycle/LifecyclePipelineBuilder.cs`
- `src/AtomUI.City.Core/Lifecycle/LifecycleScope.cs`
- `src/AtomUI.City.Core/Modularity/ModuleRegistry.cs`
- `src/AtomUI.City.Core/Modularity/ServiceConfigurationContext.cs`
- `src/AtomUI.City.Core/Modularity/ModuleBase.cs`
- `src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs`
- `src/AtomUI.City.Core/Diagnostics/HostDiagnosticRecord.cs`
- `src/AtomUI.City.Core/Diagnostics/IHostDiagnostics.cs`
- `src/AtomUI.City.Core/Diagnostics/InMemoryHostDiagnostics.cs`
- `src/AtomUI.City.Core/Threading/IUiDispatcher.cs`
- `src/AtomUI.City.Core/Threading/UnavailableUiDispatcher.cs`

Core 测试文件：

- `tests/AtomUI.City.Core.Tests/ApplicationHostBuilderTests.cs`
- `tests/AtomUI.City.Core.Tests/ApplicationHostRuntimeTests.cs`
- `tests/AtomUI.City.Core.Tests/ApplicationHostLifecycleIntegrationTests.cs`
- `tests/AtomUI.City.Core.Tests/LifecycleMiddlewarePipelineTests.cs`
- `tests/AtomUI.City.Core.Tests/LifecycleScopeTreeTests.cs`
- `tests/AtomUI.City.Core.Tests/ModuleAttributeTests.cs`
- `tests/AtomUI.City.Core.Tests/ModuleBaseTests.cs`
- `tests/AtomUI.City.Core.Tests/ModuleDescriptorTests.cs`
- `tests/AtomUI.City.Core.Tests/ApplicationHostModuleLifecycleTests.cs`
- `tests/AtomUI.City.Core.Tests/ServiceRegistrationAttributeTests.cs`
- `tests/AtomUI.City.Core.Tests/HostDiagnosticsTests.cs`
- `tests/AtomUI.City.Core.Tests/UiDispatcherIntegrationTests.cs`
- `tests/AtomUI.City.Core.Tests/CoreAssemblyTests.cs`

文档文件：

- `docs/modules/core/api-contracts.md`
- `docs/modules/core/features.md`
- `docs/modules/core/testing.md`
- `docs/modules/core/implementation-plan.md`
- `docs/engineering/implementation-roadmap.md`
- `docs/engineering/public-api-review.md`

## 执行规则

- 每个任务先写或补齐失败测试，再做最小实现。
- 每个任务完成后运行该任务的 focused test。
- Feature 未通过产品级 contract 测试前，不允许把状态改成 implemented。
- 文档和代码行为不一致时，先改 `docs/modules/core/api-contracts.md`、`docs/modules/core/features.md` 和 `docs/modules/core/testing.md`，再改实现。
- 未进入 `docs/modules/core/api-contracts.md` 的 public API 不允许在本计划中新增。
- `src/AtomUI.City.Core` 不允许新增 Avalonia、AtomUI、Roslyn、CLI、Templates、Build、Generators 或 Testing 生产引用。

## 任务

### 任务 1：建立 Phase 0 工程基线

**文件：**

- 检查：`AtomUICity.slnx`
- 检查：`Directory.Build.props`
- 检查：`Directory.Packages.props`
- 检查：`build/Common.props`
- 检查：`build/Output.props`
- 检查：`src/AtomUI.City.Core/AtomUI.City.Core.csproj`
- 检查：`tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj`

- [ ] **步骤 1：记录当前 worktree 状态**

运行：

```bash
git status --short
```

预期：可以存在用户已有的文档改动；不要回滚无关改动。本计划执行过程中只提交 Phase 0 和 Phase 1 Core 相关修改。

- [ ] **步骤 2：恢复依赖**

运行：

```bash
dotnet restore AtomUICity.slnx
```

预期：restore 成功，没有 package downgrade、缺包或 target framework 错误。

- [ ] **步骤 3：构建 solution**

运行：

```bash
dotnet build AtomUICity.slnx --no-restore
```

预期：默认 Debug 目标构建通过。

- [ ] **步骤 4：运行全量测试**

运行：

```bash
dotnet test AtomUICity.slnx --no-build
```

预期：当前测试全部通过。如果失败来自本轮无关的既有脏改动，先记录失败，不进入 Core 实现。

- [ ] **步骤 5：运行文档和 public API 门禁**

运行：

```bash
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
```

预期：文档链接、代码块和 public API XML 文档检查通过。`engineering/check-public-api.sh` 必须在 `dotnet build` 之后运行，因为它读取 `output/bin` 下的 XML 文档。

- [ ] **步骤 6：仅在基线文件发生修改时提交**

只有本任务修改了工程或构建文件时才运行：

```bash
git add AtomUICity.slnx Directory.Build.props Directory.Packages.props build src tests
git commit -m "chore: stabilize phase 0 baseline"
```

预期：如果任务只是验证，不创建提交。

### 任务 2：补齐 Core public API 方法合同

**文件：**

- 修改：`docs/modules/core/api-contracts.md`
- 修改：`docs/modules/core/features.md`
- 修改：`docs/modules/core/testing.md`
- 修改：`docs/modules/core/implementation-plan.md`

- [ ] **步骤 1：先补方法合同，再写代码**

在 `docs/modules/core/api-contracts.md` 中补齐以下方法合同：

```text
Method: ApplicationHost.CreateBuilder
Feature: AUC-CORE-001
Purpose: 创建独立 Host builder。
Parameters: args 可为空，必须 defensive copy。
Return: 新的 IApplicationHostBuilder。
Nullability: args 可以为 null；返回值不能为空。
Cancellation: 无。
Exceptions or Result: 未来 options 非法时抛 ArgumentException。
Idempotency: 每次调用返回独立 builder。
Concurrency: 静态方法没有共享可变状态。
Side Effects: 读取 generic host 默认值和命令行配置。
Diagnostics: Build 前不写诊断。
Tests: ApplicationHostBuilderTests。

Method: ApplicationHostBuilder.ConfigureServices
Feature: AUC-CORE-001
Purpose: 在 Build 前注册服务。
Parameters: configureServices 不能为 null。
Return: 当前 builder。
Nullability: delegate 不能为 null。
Cancellation: 无。
Exceptions or Result: null delegate 抛 ArgumentNullException；Build 后调用抛 InvalidOperationException。
Idempotency: Build 前多次调用按顺序追加注册。
Concurrency: 配置阶段不保证线程安全。
Side Effects: 只修改 Build 前 service collection。
Diagnostics: 如果注册导致 Build 失败，失败诊断包含注册阶段。
Tests: ApplicationHostBuilderTests。

Method: ApplicationHostBuilder.ConfigureHost
Feature: AUC-CORE-001
Purpose: 在 Build 前注册 host options 修改。
Parameters: configureOptions 不能为 null。
Return: 当前 builder。
Nullability: delegate 不能为 null。
Cancellation: 无。
Exceptions or Result: null delegate 抛 ArgumentNullException；Build 后调用抛 InvalidOperationException。
Idempotency: Build 前多次调用按注册顺序执行。
Concurrency: 配置阶段不保证线程安全。
Side Effects: 只修改 pending host options。
Diagnostics: Build 失败诊断包含 options 校验上下文。
Tests: ApplicationHostBuilderTests; ApplicationHostOptionsTests。

Method: ApplicationHostBuilder.Build
Feature: AUC-CORE-001
Purpose: 冻结 builder，配置 modules，构建 root provider，并创建 Host。
Parameters: 无。
Return: 新的 IApplicationHost。
Nullability: 返回 Host 不能为空。
Cancellation: 无。
Exceptions or Result: 重复 Build 或 module graph 非法抛 InvalidOperationException；module 配置异常写入 diagnostics 后重新抛出。
Idempotency: Build 只允许成功一次，后续调用必须失败。
Concurrency: Build 必须外部串行。
Side Effects: 冻结 Services、Configuration、Properties 和注册方法；成功时写 HostBuilt。
Diagnostics: 成功写 AUCHOST001；失败写 AUCHOST101。
Tests: ApplicationHostBuilderTests; HostDiagnosticsTests。

Method: IApplicationHost.StartAsync
Feature: AUC-CORE-002
Purpose: 启动 generic host，创建 application scope，配置 contributions，并初始化 modules。
Parameters: cancellationToken 控制启动等待和 module 初始化。
Return: Host 进入 Running 后完成的 Task。
Nullability: 返回 task 不能为空。
Cancellation: 完成前取消时 Host 必须保持稳定且可释放。
Exceptions or Result: Dispose 后抛 ObjectDisposedException；Stop 后再启动抛 InvalidOperationException；启动失败写 diagnostics 后重新抛出。
Idempotency: Running 状态重复调用不重新执行启动流程。
Concurrency: 并发调用由 Host state lock 串行化。
Side Effects: 创建 ApplicationScope，并且只执行一次 module initialization。
Diagnostics: 成功写 AUCHOST002；失败写 AUCHOST102。
Tests: ApplicationHostRuntimeTests; ApplicationHostLifecycleIntegrationTests。

Method: IApplicationHost.StopAsync
Feature: AUC-CORE-002
Purpose: 关闭 modules，停止 application 和 host scopes，并停止 generic host。
Parameters: cancellationToken 控制等待，但不能跳过已经开始的最小清理。
Return: Host 停止后完成的 Task。
Nullability: 返回 task 不能为空。
Cancellation: 取消只影响可取消等待；清理失败必须写 diagnostics。
Exceptions or Result: Dispose 后抛 ObjectDisposedException；shutdown 失败聚合并写 diagnostics。
Idempotency: Stopped 后重复调用不重新执行 shutdown。
Concurrency: 并发调用由同一个停止事务处理。
Side Effects: 取消 application 和 host scopes，成功时写 HostStopped。
Diagnostics: 成功写 AUCHOST003；失败写 AUCHOST103。
Tests: ApplicationHostRuntimeTests; ApplicationHostLifecycleIntegrationTests。

Method: LifecycleScope.CreateChild
Feature: AUC-CORE-003
Purpose: 创建链接父级 cancellation 的 child scope。
Parameters: kind 和 id 标识 child scope。
Return: 新的 LifecycleScope。
Nullability: id 不能为 null 或空白。
Cancellation: child token 链接 parent token。
Exceptions or Result: parent 处于 stopping、stopped、disposing 或 disposed 时抛 InvalidOperationException。
Idempotency: parent running 时每次调用创建一个 child。
Concurrency: child 创建由 scope lock 保护。
Side Effects: 把 child 加入 parent 的 Children 快照来源。
Diagnostics: Host-owned cleanup 报告非法 scope mutation 时写 AUCHOST104。
Tests: LifecycleScopeTreeTests。

Method: LifecycleScope.StopAsync
Feature: AUC-CORE-003
Purpose: 取消当前 scope，并 leaf-first 停止 child scopes。
Parameters: 无。
Return: scope 停止后完成的 ValueTask。
Nullability: 返回 ValueTask 有效。
Cancellation: Stop 会取消 scope token；不接受外部 cancellation。
Exceptions or Result: 重复 Stop 是 no-op；child failure 在挂接 diagnostics sink 时写 diagnostics。
Idempotency: 重复调用不改变顺序，也不重复取消。
Concurrency: 并发调用由 scope lock 串行化。
Side Effects: State 从 Running 变为 Stopping，再变为 Stopped。
Diagnostics: child stop 或 dispose 失败写 AUCHOST104。
Tests: LifecycleScopeTreeTests。
```

- [ ] **步骤 2：明确 Host restart 合同**

把产品合同写成：

```text
IApplicationHost 只能从 Created 状态启动。进入 Stopped 后只能 Dispose，不能再次 Start。
```

预期文档变化：

- `docs/modules/core/api-contracts.md` 写明 `StartAsync` 在 `Stopped` 后抛 `InvalidOperationException`。
- `docs/modules/core/features.md` 保留 Stop 幂等，不承诺 restart。
- `docs/modules/core/testing.md` 在 `AUC-CORE-002` 下加入 restart rejection 断言。

- [ ] **步骤 3：运行文档门禁**

运行：

```bash
bash engineering/check-docs.sh
```

预期：没有断链、奇数代码块或禁止 token。

- [ ] **步骤 4：提交 API 合同更新**

运行：

```bash
git add docs/modules/core/api-contracts.md docs/modules/core/features.md docs/modules/core/testing.md docs/modules/core/implementation-plan.md
git commit -m "docs: freeze core phase 1 method contracts"
```

预期：提交只包含文档修改。

### 任务 3：实现 Diagnostics 基础

**文件：**

- 修改：`src/AtomUI.City.Core/Diagnostics/HostDiagnosticIds.cs`
- 修改：`src/AtomUI.City.Core/Diagnostics/HostDiagnosticRecord.cs`
- 修改：`src/AtomUI.City.Core/Diagnostics/InMemoryHostDiagnostics.cs`
- 修改：`tests/AtomUI.City.Core.Tests/HostDiagnosticsTests.cs`

- [ ] **步骤 1：写失败测试**

新增稳定诊断码和不可变 context 测试：

```csharp
[Fact]
public void HostDiagnosticIdsIncludePhaseOneFailureCodes()
{
    Assert.Equal("AUCHOST001", HostDiagnosticIds.HostBuilt);
    Assert.Equal("AUCHOST002", HostDiagnosticIds.HostStarted);
    Assert.Equal("AUCHOST003", HostDiagnosticIds.HostStopped);
    Assert.Equal("AUCHOST101", HostDiagnosticIds.HostBuildFailed);
    Assert.Equal("AUCHOST102", HostDiagnosticIds.HostStartFailed);
    Assert.Equal("AUCHOST103", HostDiagnosticIds.HostStopFailed);
    Assert.Equal("AUCHOST104", HostDiagnosticIds.LifecycleScopeCleanupFailed);
    Assert.Equal("AUCHOST105", HostDiagnosticIds.ModuleGraphFailed);
    Assert.Equal("AUCHOST106", HostDiagnosticIds.ModuleLifecycleFailed);
    Assert.Equal("AUCHOST107", HostDiagnosticIds.DispatcherUnavailable);
}

[Fact]
public void DiagnosticContextRejectsExternalMutation()
{
    var context = new Dictionary<string, string?>
    {
        ["moduleId"] = "SampleModule"
    };

    var record = new HostDiagnosticRecord(
        HostDiagnosticIds.ModuleLifecycleFailed,
        "Module failed.",
        HostDiagnosticSeverity.Error)
    {
        Context = context
    };

    context["moduleId"] = "Changed";

    Assert.Equal("SampleModule", record.Context["moduleId"]);
    Assert.Throws<NotSupportedException>(() =>
        Assert.IsAssignableFrom<IDictionary<string, string?>>(record.Context)["moduleId"] = "ChangedAgain");
}
```

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --no-build --filter HostDiagnosticsTests
```

预期：新 diagnostic ids 和 `Context` 尚未实现，测试失败。

- [ ] **步骤 2：实现诊断码和不可变 context**

目标 public surface：

```csharp
public static class HostDiagnosticIds
{
    public const string HostBuilt = "AUCHOST001";
    public const string HostStarted = "AUCHOST002";
    public const string HostStopped = "AUCHOST003";
    public const string HostBuildFailed = "AUCHOST101";
    public const string HostStartFailed = "AUCHOST102";
    public const string HostStopFailed = "AUCHOST103";
    public const string LifecycleScopeCleanupFailed = "AUCHOST104";
    public const string ModuleGraphFailed = "AUCHOST105";
    public const string ModuleLifecycleFailed = "AUCHOST106";
    public const string DispatcherUnavailable = "AUCHOST107";
}
```

`HostDiagnosticRecord` 保留现有 positional constructor 兼容性，并新增 defensive copy 的 `Context`：

```csharp
public sealed record HostDiagnosticRecord(
    string Code,
    string Message,
    HostDiagnosticSeverity Severity,
    string? ScopeId = null,
    LifecycleStage? Stage = null)
{
    private IReadOnlyDictionary<string, string?> _context =
        new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>());

    public IReadOnlyDictionary<string, string?> Context
    {
        get => _context;
        init => _context = new ReadOnlyDictionary<string, string?>(
            new Dictionary<string, string?>(value, StringComparer.Ordinal));
    }
}
```

- [ ] **步骤 3：运行 diagnostics 测试**

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --filter HostDiagnosticsTests
```

预期：`HostDiagnosticsTests` 全部通过。

- [ ] **步骤 4：提交 diagnostics 基础**

运行：

```bash
git add src/AtomUI.City.Core/Diagnostics tests/AtomUI.City.Core.Tests/HostDiagnosticsTests.cs
git commit -m "feat: add core host failure diagnostics"
```

预期：提交只包含诊断码、诊断 record 和对应测试。

### 任务 4：实现 Host Builder 冻结

**文件：**

- 修改：`src/AtomUI.City.Core/Hosting/ApplicationHostBuilder.cs`
- 修改：`src/AtomUI.City.Core/Hosting/IApplicationHostBuilder.cs`
- 修改：`tests/AtomUI.City.Core.Tests/ApplicationHostBuilderTests.cs`

- [ ] **步骤 1：写 builder freeze 失败测试**

新增覆盖所有 public mutation 入口的测试：

```csharp
[Fact]
public async Task BuildFreezesPublicBuilderMutationEntrypoints()
{
    var builder = ApplicationHost.CreateBuilder();

    await using var host = builder.Build();

    Assert.Throws<InvalidOperationException>(() =>
        builder.ConfigureServices(services => services.AddSingleton<TestService>()));
    Assert.Throws<InvalidOperationException>(() =>
        builder.ConfigureHost(options => options.ApplicationName = "changed"));
    Assert.Throws<InvalidOperationException>(() =>
        builder.Services.AddSingleton<TestService>());
    Assert.Throws<InvalidOperationException>(() =>
        builder.Properties["changed"] = true);
    Assert.Throws<InvalidOperationException>(() =>
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["changed"] = "true" }));
}

[Fact]
public async Task BuildCanOnlyRunOnce()
{
    var builder = ApplicationHost.CreateBuilder();

    await using var host = builder.Build();

    var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
    Assert.Contains("only build once", exception.Message, StringComparison.OrdinalIgnoreCase);
}
```

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --no-build --filter ApplicationHostBuilderTests
```

预期：`Services`、`Properties` 或 `Configuration` 在 Build 后仍可变，测试失败。

- [ ] **步骤 2：实现 guarded wrappers**

在 `ApplicationHostBuilder` 中加入冻结检查：

```csharp
private void ThrowIfBuilt()
{
    if (_built)
    {
        throw new InvalidOperationException("Application host builder is frozen after Build.");
    }
}
```

实现要求：

- `ConfigureServices` 调用 delegate 前必须调用 `ThrowIfBuilt()`。
- `ConfigureHost` 存储 delegate 前必须调用 `ThrowIfBuilt()`。
- `Services` 返回 wrapper，拦截 `Add`、`Insert`、`Remove`、`Clear`、indexer set 和 `RemoveAt`。
- `Properties` 返回 wrapper，拦截 `Add`、`Remove`、`Clear` 和 indexer set。
- `Configuration` 返回 wrapper 或等价保护，确保 public builder 在 Build 后不能修改 configuration sources。
- `Build` 内部框架注册继续使用底层 `HostApplicationBuilder.Services`，避免冻结 wrapper 阻断内部注册。

- [ ] **步骤 3：确认 Build 成功诊断仍存在**

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --filter "ApplicationHostBuilderTests|HostDiagnosticsTests"
```

预期：builder 和 diagnostics 测试全部通过，且仍能找到 `AUCHOST001`。

- [ ] **步骤 4：提交 builder freeze**

运行：

```bash
git add src/AtomUI.City.Core/Hosting tests/AtomUI.City.Core.Tests/ApplicationHostBuilderTests.cs
git commit -m "feat: freeze application host builder after build"
```

预期：提交只包含 Host builder freeze 实现和测试。

### 任务 5：实现 Host 生命周期产品语义

**文件：**

- 修改：`src/AtomUI.City.Core/Hosting/DefaultApplicationHost.cs`
- 修改：`tests/AtomUI.City.Core.Tests/ApplicationHostRuntimeTests.cs`
- 修改：`tests/AtomUI.City.Core.Tests/ApplicationHostLifecycleIntegrationTests.cs`

- [ ] **步骤 1：写 lifecycle 失败测试**

新增以下 contract tests：

```csharp
[Fact]
public async Task StartAfterStopIsRejectedAndDisposeStillSucceeds()
{
    await using var host = ApplicationHost.CreateBuilder().Build();

    await host.StartAsync();
    await host.StopAsync();

    await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());
    await host.DisposeAsync();
}

[Fact]
public async Task StartupFailureIsRecordedAndHostCanBeDisposed()
{
    var builder = ApplicationHost.CreateBuilder();
    builder.ConfigureServices(services =>
    {
        services.AddSingleton<IHostedService, FailingHostedService>();
    });

    await using var host = builder.Build();

    await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

    var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
    Assert.Contains(diagnostics.Records, record =>
        record.Code == HostDiagnosticIds.HostStartFailed &&
        record.Context["exceptionType"] == typeof(InvalidOperationException).FullName);

    await host.StopAsync();
}

private sealed class FailingHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("startup failed");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --no-build --filter "ApplicationHostRuntimeTests|ApplicationHostLifecycleIntegrationTests"
```

预期：失败诊断和失败后可释放断言尚未实现，测试失败。

- [ ] **步骤 2：强化 Host 状态机**

实现要求：

- `StartAsync` 在已经 running 时直接返回。
- `StartAsync` 在成功 stop 之后抛 `InvalidOperationException`。
- `StartAsync` 启动失败时写 `AUCHOST102`，然后重新抛出。
- `StopAsync` 在未启动或已停止时直接返回。
- `StopAsync` 只执行一次 module shutdown、application scope stop、host scope stop 和 generic host stop。
- `StopAsync` 停止失败时写 `AUCHOST103`，并保持 Host 可 dispose。
- `Dispose` 和 `DisposeAsync` 幂等。

增加私有诊断辅助方法，避免 diagnostics collector 失败阻断清理：

```csharp
private void WriteDiagnostic(HostDiagnosticRecord record)
{
    try
    {
        _diagnostics.Write(record);
    }
    catch
    {
        // 诊断失败不能阻断生命周期清理。
    }
}
```

- [ ] **步骤 3：运行 lifecycle 测试**

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --filter "ApplicationHostRuntimeTests|ApplicationHostLifecycleIntegrationTests|HostDiagnosticsTests"
```

预期：Host lifecycle、integration 和 diagnostics 测试全部通过。

- [ ] **步骤 4：提交 Host 生命周期语义**

运行：

```bash
git add src/AtomUI.City.Core/Hosting/DefaultApplicationHost.cs tests/AtomUI.City.Core.Tests/ApplicationHostRuntimeTests.cs tests/AtomUI.City.Core.Tests/ApplicationHostLifecycleIntegrationTests.cs
git commit -m "feat: harden core host lifecycle semantics"
```

预期：提交包含 Host 生命周期实现和测试。

### 任务 6：实现 LifecycleScope 产品语义

**文件：**

- 修改：`src/AtomUI.City.Core/Lifecycle/LifecycleScope.cs`
- 创建或修改：`src/AtomUI.City.Core/Properties/AssemblyInfo.cs`
- 修改：`tests/AtomUI.City.Core.Tests/LifecycleScopeTreeTests.cs`

- [ ] **步骤 1：写 scope 失败测试**

新增以下测试：

```csharp
[Fact]
public async Task DisposeStopsAndDisposesChildrenLeafFirst()
{
    var order = new List<string>();
    await using var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");
    var application = host.CreateChild(LifecycleScopeKind.Application, "application");
    var operation = application.CreateChild(LifecycleScopeKind.Operation, "operation");

    operation.Disposed += (_, _) => order.Add("operation");
    application.Disposed += (_, _) => order.Add("application");
    host.Disposed += (_, _) => order.Add("host");

    await host.DisposeAsync();

    Assert.Equal(["operation", "application", "host"], order);
    Assert.Equal(LifecycleScopeState.Disposed, host.State);
    Assert.Equal(LifecycleScopeState.Disposed, application.State);
    Assert.Equal(LifecycleScopeState.Disposed, operation.State);
}

[Fact]
public async Task ConcurrentStopAndCreateChildDoNotCorruptScopeState()
{
    await using var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");

    var stopTask = host.StopAsync().AsTask();
    await stopTask;

    Assert.Throws<InvalidOperationException>(() =>
        host.CreateChild(LifecycleScopeKind.Operation, "late-operation"));
    Assert.Equal(LifecycleScopeState.Stopped, host.State);
}

[Fact]
public async Task DisposeIsIdempotent()
{
    var host = LifecycleScope.CreateRoot(LifecycleScopeKind.Host, "host");

    await host.DisposeAsync();
    await host.DisposeAsync();
    host.Dispose();

    Assert.Equal(LifecycleScopeState.Disposed, host.State);
}
```

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --no-build --filter LifecycleScopeTreeTests
```

预期：内部 `Disposed` 事件和同步 mutation 保护尚未实现，测试失败。

- [ ] **步骤 2：实现同步 scope ownership**

实现要求：

- 增加私有 lock，保护 `_children`、`_disposed` 和 `State`。
- `Children` 在 lock 内创建快照。
- `CreateChild` 在 state 不是 `Running` 时失败。
- `StopAsync`、`Dispose` 和 `DisposeAsync` 幂等。
- child scopes 按 reverse creation order stop 和 dispose。
- child disposal 和 token source disposal 完成后触发内部 `Disposed` 事件。

新增内部事件：

```csharp
internal event EventHandler? Disposed;
```

如果 `src/AtomUI.City.Core/Properties/AssemblyInfo.cs` 不存在，创建：

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AtomUI.City.Core.Tests")]
```

- [ ] **步骤 3：运行 scope 测试**

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --filter LifecycleScopeTreeTests
```

预期：lifecycle scope 测试全部通过。

- [ ] **步骤 4：提交 lifecycle scope 语义**

运行：

```bash
git add src/AtomUI.City.Core/Lifecycle/LifecycleScope.cs src/AtomUI.City.Core/Properties/AssemblyInfo.cs tests/AtomUI.City.Core.Tests/LifecycleScopeTreeTests.cs
git commit -m "feat: make lifecycle scopes deterministic"
```

预期：提交只包含 lifecycle scope 实现和测试。

### 任务 7：实现 Module graph 和 lifecycle 合同

**文件：**

- 修改：`src/AtomUI.City.Core/Modularity/ModuleRegistry.cs`
- 修改：`src/AtomUI.City.Core/Modularity/ServiceConfigurationContext.cs`
- 修改：`src/AtomUI.City.Core/Modularity/ModuleBase.cs`
- 修改：`tests/AtomUI.City.Core.Tests/ModuleAttributeTests.cs`
- 修改：`tests/AtomUI.City.Core.Tests/ModuleBaseTests.cs`
- 修改：`tests/AtomUI.City.Core.Tests/ModuleDescriptorTests.cs`
- 修改：`tests/AtomUI.City.Core.Tests/ApplicationHostModuleLifecycleTests.cs`

- [ ] **步骤 1：写 module graph 失败测试**

新增默认 id、显式 id、缺失依赖、循环依赖和 shutdown 顺序测试：

```csharp
[Fact]
public void ModuleGraphFailureRecordsMissingDependencyPath()
{
    var exception = Assert.Throws<InvalidOperationException>(() =>
        ModuleRegistry.CreateForTesting([typeof(DependsOnMissingModule)]));

    Assert.Contains(typeof(DependsOnMissingModule).FullName, exception.Message);
    Assert.Contains(typeof(MissingModule).FullName, exception.Message);
}

[Fact]
public async Task ModuleShutdownRunsInReverseDependencyOrder()
{
    ModuleRecorder.Reset();
    var builder = ApplicationHost.CreateBuilder();
    builder.UseModule<FoundationModule>();
    builder.UseModule<FeatureModule>();

    await using var host = builder.Build();

    await host.StartAsync();
    await host.StopAsync();

    Assert.Equal(
        ["foundation:init", "feature:init", "feature:shutdown", "foundation:shutdown"],
        ModuleRecorder.Calls);
}
```

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --no-build --filter "ModuleDescriptorTests|ApplicationHostModuleLifecycleTests|ModuleBaseTests|ModuleAttributeTests"
```

预期：failure path diagnostics 和 test factory 尚未完整实现，测试失败。

- [ ] **步骤 2：增加内部测试入口，不新增生产 public API**

通过 `InternalsVisibleTo` 或现有 internal 测试方式暴露 graph construction。生产 public API 保持不变。

内部方法形状：

```csharp
internal static ModuleRegistry CreateForTesting(IReadOnlyList<Type> moduleTypes)
{
    var registrations = moduleTypes
        .Select(moduleType => new ModuleRegistration(moduleType, () => (IModule)Activator.CreateInstance(moduleType)!))
        .ToArray();

    return Create(registrations);
}
```

- [ ] **步骤 3：实现 graph 和 lifecycle 失败语义**

实现要求：

- duplicate module id 在 graph creation 阶段失败。
- missing required dependency 失败，并包含 source module 和 missing module type。
- cyclic dependency 失败，并包含 cycle path。
- optional missing dependency 不失败。
- initialization phases 按 dependency order 执行。
- shutdown 按 reverse dependency order 执行。
- module lifecycle failure 写 `AUCHOST106`，context 包含 `moduleId`、`moduleType` 和 `stage`。

- [ ] **步骤 4：阻止 module service configuration 创建临时 provider**

把 `ServiceConfigurationContext.Services` 改为 Core-owned registration collection 类型，仍支持服务注册，但阻止 module author 直接调用临时 provider 创建入口：

```csharp
public sealed class ModuleServiceCollection : IServiceCollection
{
    private readonly IServiceCollection _inner;

    internal ModuleServiceCollection(IServiceCollection inner)
    {
        _inner = inner;
    }

    public ServiceDescriptor this[int index]
    {
        get => _inner[index];
        set => _inner[index] = value;
    }

    public int Count => _inner.Count;
    public bool IsReadOnly => _inner.IsReadOnly;
    public void Add(ServiceDescriptor item) => _inner.Add(item);
    public void Clear() => _inner.Clear();
    public bool Contains(ServiceDescriptor item) => _inner.Contains(item);
    public void CopyTo(ServiceDescriptor[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
    public IEnumerator<ServiceDescriptor> GetEnumerator() => _inner.GetEnumerator();
    public int IndexOf(ServiceDescriptor item) => _inner.IndexOf(item);
    public void Insert(int index, ServiceDescriptor item) => _inner.Insert(index, item);
    public bool Remove(ServiceDescriptor item) => _inner.Remove(item);
    public void RemoveAt(int index) => _inner.RemoveAt(index);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class ModuleServiceCollectionBuildGuardExtensions
{
    public static ServiceProvider BuildServiceProvider(this ModuleServiceCollection services)
    {
        throw new InvalidOperationException("Modules must not build a temporary service provider during service configuration.");
    }
}
```

新增测试 module 调用 `context.Services.BuildServiceProvider()`，断言 Host Build 失败，异常为 guard exception，并写入 `AUCHOST106`。

- [ ] **步骤 5：运行 module 测试**

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --filter "ModuleDescriptorTests|ApplicationHostModuleLifecycleTests|ModuleBaseTests|ModuleAttributeTests"
```

预期：module 测试全部通过。

- [ ] **步骤 6：提交 module graph 和 lifecycle 合同**

运行：

```bash
git add src/AtomUI.City.Core/Modularity tests/AtomUI.City.Core.Tests/ModuleAttributeTests.cs tests/AtomUI.City.Core.Tests/ModuleBaseTests.cs tests/AtomUI.City.Core.Tests/ModuleDescriptorTests.cs tests/AtomUI.City.Core.Tests/ApplicationHostModuleLifecycleTests.cs
git commit -m "feat: enforce core module graph contracts"
```

预期：提交包含 module graph、lifecycle 和测试。

### 任务 8：补齐 DI marker 合同测试

**文件：**

- 修改：`src/AtomUI.City.Core/DependencyInjection/ServiceAttribute.cs`
- 修改：`src/AtomUI.City.Core/DependencyInjection/ScopedServiceAttribute.cs`
- 修改：`src/AtomUI.City.Core/DependencyInjection/ExposeServicesAttribute.cs`
- 修改：`tests/AtomUI.City.Core.Tests/ServiceRegistrationAttributeTests.cs`

- [ ] **步骤 1：写 DI marker 失败测试**

新增 lifetime metadata、exposed services、conflict 和 AOT-readable attribute 测试：

```csharp
[Fact]
public void ServiceAttributesExposeAotReadableMetadata()
{
    var service = typeof(SampleSingletonService).GetCustomAttribute<ServiceAttribute>();
    var exposed = typeof(SampleSingletonService).GetCustomAttribute<ExposeServicesAttribute>();

    Assert.NotNull(service);
    Assert.Equal(ServiceLifetime.Singleton, service.Lifetime);
    Assert.Equal([typeof(ISampleService)], exposed!.ServiceTypes);
}

[Fact]
public void ConflictingLifetimeMarkersAreRejectedByMetadataReader()
{
    var exception = Assert.Throws<InvalidOperationException>(() =>
        ServiceRegistrationMetadata.Read(typeof(ConflictingLifetimeService)));

    Assert.Contains(nameof(ConflictingLifetimeService), exception.Message);
}
```

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --no-build --filter ServiceRegistrationAttributeTests
```

预期：metadata validation 缺失时 conflict reader 测试失败。

- [ ] **步骤 2：在现有 attribute 无法表达合同时实现 metadata reader**

实现要求：

- `ServiceAttribute` lifetime 只有在没有 marker lifetime interface 冲突时生效。
- `ISingletonDependency`、`IScopedDependency` 和 `ITransientDependency` 互斥。
- `ExposeServicesAttribute.ServiceTypes` 返回 defensive immutable snapshot。
- exposed service type 不是 implementation type 可赋值接口或基类时失败。

- [ ] **步骤 3：运行 DI marker 测试**

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --filter ServiceRegistrationAttributeTests
```

预期：DI marker 测试全部通过。

- [ ] **步骤 4：提交 DI marker 合同**

运行：

```bash
git add src/AtomUI.City.Core/DependencyInjection tests/AtomUI.City.Core.Tests/ServiceRegistrationAttributeTests.cs
git commit -m "feat: validate core service registration markers"
```

预期：提交包含 DI marker validation 和测试。

### 任务 9：补齐 UI Dispatcher 和 assembly boundary 合同

**文件：**

- 修改：`src/AtomUI.City.Core/Threading/IUiDispatcher.cs`
- 修改：`src/AtomUI.City.Core/Threading/UnavailableUiDispatcher.cs`
- 修改：`tests/AtomUI.City.Core.Tests/UiDispatcherIntegrationTests.cs`
- 修改：`tests/AtomUI.City.Core.Tests/CoreAssemblyTests.cs`

- [ ] **步骤 1：写 dispatcher 失败测试**

新增 unavailable dispatcher cancellation、diagnostic code 和 package boundary 测试：

```csharp
[Fact]
public async Task UnavailableDispatcherRejectsDispatchWithStableDiagnosticCode()
{
    var dispatcher = new UnavailableUiDispatcher();

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        dispatcher.InvokeAsync(() => { }).AsTask());

    Assert.Contains(HostDiagnosticIds.DispatcherUnavailable, exception.Message);
}

[Fact]
public void CoreAssemblyDoesNotReferenceUiBuildOrTestingAssemblies()
{
    var referenced = typeof(ApplicationHost).Assembly
        .GetReferencedAssemblies()
        .Select(name => name.Name)
        .ToArray();

    Assert.DoesNotContain("Avalonia", referenced);
    Assert.DoesNotContain("AtomUI", referenced);
    Assert.DoesNotContain("Microsoft.CodeAnalysis.CSharp", referenced);
    Assert.DoesNotContain("AtomUI.City.Cli", referenced);
    Assert.DoesNotContain("AtomUI.City.Templates", referenced);
    Assert.DoesNotContain("AtomUI.City.Testing", referenced);
}
```

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --no-build --filter "UiDispatcherIntegrationTests|CoreAssemblyTests"
```

预期：如果 unavailable dispatcher 未包含 `AUCHOST107`，diagnostic-code 断言失败。

- [ ] **步骤 2：实现 dispatcher failure 合同**

实现要求：

- `UnavailableUiDispatcher` 不能执行传入的 UI work。
- dispatch 必须抛 `InvalidOperationException`。
- exception message 必须包含 `AUCHOST107`。
- dispatch 前 token 已取消时必须抛 `OperationCanceledException`。
- Core 项目引用只保留允许的 `Microsoft.Extensions` 包。

- [ ] **步骤 3：运行 dispatcher 和 assembly 测试**

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --filter "UiDispatcherIntegrationTests|CoreAssemblyTests"
```

预期：dispatcher 和 assembly boundary 测试全部通过。

- [ ] **步骤 4：提交 dispatcher 和 boundary 合同**

运行：

```bash
git add src/AtomUI.City.Core/Threading tests/AtomUI.City.Core.Tests/UiDispatcherIntegrationTests.cs tests/AtomUI.City.Core.Tests/CoreAssemblyTests.cs
git commit -m "feat: enforce core dispatcher boundary"
```

预期：提交包含 dispatcher 行为和 assembly tests。

### 任务 10：最终 Core 产品门禁

**文件：**

- 修改：`docs/modules/core/implementation-plan.md`
- 检查：任务 2 到任务 9 修改过的全部文件。

- [ ] **步骤 1：运行 focused Core 测试**

运行：

```bash
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj
```

预期：Core 测试全部通过。

- [ ] **步骤 2：运行完整验证**

运行：

```bash
dotnet build AtomUICity.slnx
dotnet test AtomUICity.slnx --no-build
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
```

预期：build、tests、文档门禁、public API 门禁和 whitespace 检查全部通过。

- [ ] **步骤 3：更新 Core 实现状态**

只有步骤 2 全部通过后，才更新 `docs/modules/core/implementation-plan.md`：

```text
AUC-CORE-001 Status: Implemented and Product Contract Tested
AUC-CORE-002 Status: Implemented and Product Contract Tested
AUC-CORE-003 Status: Implemented and Product Contract Tested
AUC-CORE-004 Status: Implemented and Product Contract Tested
AUC-CORE-005 Status: Implemented and Product Contract Tested
AUC-CORE-006 Status: Implemented and Product Contract Tested
AUC-CORE-007 Status: Implemented and Product Contract Tested
```

没有通过测试或文档验证的 feature 不允许更新状态。

- [ ] **步骤 4：提交最终状态更新**

运行：

```bash
git add docs/modules/core/implementation-plan.md
git commit -m "docs: mark core phase 1 product contracts verified"
```

预期：提交只包含 Core status tracking。

## 完成标准

本计划完成时必须满足：

- `dotnet build AtomUICity.slnx` 通过。
- `dotnet test AtomUICity.slnx --no-build` 通过。
- `dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj` 通过。
- `bash engineering/check-docs.sh` 通过。
- `bash engineering/check-public-api.sh` 通过。
- `git diff --check` 通过。
- `src/AtomUI.City.Core` 没有 UI、build-time、CLI、template、generator 或 testing 生产引用。
- `docs/modules/core/implementation-plan.md` 只把已验证 feature 标记为 implemented。

## 执行交接

计划已保存到 `docs/superpowers/plans/2026-06-13-phase-0-1-core-kernel-plan.md`。

执行方式：

1. **Subagent-Driven**：每个任务派发一个独立执行单元，任务后 review，提交保持小颗粒度。
2. **Inline Execution**：在当前会话内按任务推进，在文档、diagnostics、Host lifecycle、module lifecycle 和最终门禁后设置检查点。
