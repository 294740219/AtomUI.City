# Phase 2 Testing Infrastructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 `AtomUI.City.Testing` 的首个产品级测试基础设施闭环，使后续模块可以不依赖真实 UI、不依赖固定 `Task.Delay`，并能断言测试 Host、dispatcher 和虚拟时间调度的失败诊断与释放语义。

**Architecture:** 本计划只覆盖 `AUC-TESTING-001`、`AUC-TESTING-002` 和 `AUC-TESTING-003`。先冻结 Testing 文档中的关键方法合同，再用 TDD 补齐 `TestHost`、`FakeUiDispatcher` 和 `DeterministicScheduler` 的产品级行为，最后更新 Testing 状态矩阵并运行 Testing、文档和 public API 门禁。

**Tech Stack:** .NET `net10.0` Debug 目标、`AtomUI.City.Core` 的 `IUiDispatcher` contract、xUnit、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 范围决策

包含：

- `AUC-TESTING-001 Test Host`：builder 冻结、重复 Build 拒绝、Dispose 后 mutating API 拒绝、Stop 幂等、释放诊断。
- `AUC-TESTING-002 Fake Dispatcher`：实现 Core `IUiDispatcher`、FIFO drain、UI thread marker、取消、异常诊断、pending 断言。
- `AUC-TESTING-003 Deterministic Scheduler`：同一 due time 稳定排序、取消、异常诊断、Dispose 后拒绝 schedule。
- Testing 文档状态从 `Ready to Start Product Implementation` 推进到已验证范围。

不包含：

- `AUC-TESTING-004` 到 `AUC-TESTING-009` 的完整产品实现。
- 真实 UI 平台集成、真实插件包构建、Roslyn generator runner 和模板 smoke test。
- 未写入 `docs/modules/testing/api-contracts.md` 的 public API。

## 文件地图

实现文件：

- `src/AtomUI.City.Testing/TestHostBuilder.cs`
- `src/AtomUI.City.Testing/TestHost.cs`
- `src/AtomUI.City.Testing/FakeUiDispatcher.cs`
- `src/AtomUI.City.Testing/FakeUiWorkItem.cs`
- `src/AtomUI.City.Testing/DeterministicScheduler.cs`
- `src/AtomUI.City.Testing/TestDiagnostics.cs`

测试文件：

- `tests/AtomUI.City.Testing.Tests/TestHostTests.cs`
- `tests/AtomUI.City.Testing.Tests/FakeUiDispatcherTests.cs`
- `tests/AtomUI.City.Testing.Tests/SharedTestUtilitiesTests.cs`

文档文件：

- `docs/modules/testing/api-contracts.md`
- `docs/modules/testing/features.md`
- `docs/modules/testing/implementation-plan.md`
- `docs/superpowers/plans/2026-06-14-phase-2-testing-infrastructure-plan.md`

## 执行规则

- 每个任务先写失败测试，再写最小实现。
- 每个新增 public member 必须先更新 `docs/modules/testing/api-contracts.md`。
- 每个任务完成后运行 focused test，并在提交前运行相关 Testing 测试。
- Feature 未通过产品级 contract 测试前，不允许把状态改成 Implemented。
- 所有新增或修改文档必须使用中文正文；本计划保留 superpowers 规定的英文模板头。
- `src/AtomUI.City.*` 生产项目不得引用 `AtomUI.City.Testing`。

## 任务

### 任务 1：冻结 Testing 001-003 方法合同

**文件：**

- 修改：`docs/modules/testing/api-contracts.md`
- 修改：`docs/modules/testing/features.md`
- 修改：`docs/modules/testing/implementation-plan.md`

- [ ] **步骤 1：补齐关键方法合同**

在 `docs/modules/testing/api-contracts.md` 的“关键方法合同”中补齐以下方法或成员级合同：

```text
TestHostBuilder.UseProperty
TestHostBuilder.UseDirectoryName
TestHostBuilder.KeepDirectoryOnDispose
TestHostBuilder.Build
TestHost.StopAsync
TestHost.Dispose
TestHost.DisposeAsync
FakeUiDispatcher.CheckAccess
FakeUiDispatcher.Post
FakeUiDispatcher.Drain
FakeUiDispatcher.InvokeAsync
FakeUiDispatcher.PostAsync
DeterministicScheduler.Schedule
DeterministicScheduler.AdvanceBy
DeterministicScheduler.RunDueWork
```

每个合同必须包含用途、参数、返回值、空值规则、取消、异常或失败结果、幂等、并发、边界副作用、诊断和主测试文件。

- [ ] **步骤 2：运行文档检查**

运行：

```bash
bash engineering/check-docs.sh
```

预期：文档链接和代码块检查通过。

- [ ] **步骤 3：提交合同冻结**

运行：

```bash
git add docs/modules/testing/api-contracts.md docs/modules/testing/features.md docs/modules/testing/implementation-plan.md docs/superpowers/plans/2026-06-14-phase-2-testing-infrastructure-plan.md
git commit -m "docs: freeze testing phase 2 contracts"
```

预期：只提交 Testing 文档和本计划。

### 任务 2：实现 TestHost 产品级 builder 和释放语义

**文件：**

- 修改：`tests/AtomUI.City.Testing.Tests/TestHostTests.cs`
- 修改：`src/AtomUI.City.Testing/TestHostBuilder.cs`
- 修改：`src/AtomUI.City.Testing/TestHost.cs`

- [ ] **步骤 1：写 builder 冻结失败测试**

在 `TestHostTests` 中添加：

```csharp
[Fact]
public void BuildFreezesBuilderMutationEntrypoints()
{
    var builder = TestHost.CreateBuilder()
        .UseProperty("environment", "test");

    using var host = builder.Build();

    Assert.Throws<InvalidOperationException>(() => builder.UseProperty("next", "value"));
    Assert.Throws<InvalidOperationException>(() => builder.UseDirectoryName("next"));
    Assert.Throws<InvalidOperationException>(() => builder.KeepDirectoryOnDispose());
    Assert.Throws<InvalidOperationException>(() => builder.Build());
    Assert.Equal("test", host.ApplicationContext.Properties["environment"]);
}
```

- [ ] **步骤 2：运行失败测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter BuildFreezesBuilderMutationEntrypoints
```

预期：失败，原因是 builder Build 后仍可 mutation 或重复 Build。

- [ ] **步骤 3：写最小实现**

在 `TestHostBuilder` 中加入 `_built` 状态和 `ThrowIfBuilt()`，所有 mutating API 和 `Build()` 成功后必须冻结。`Build()` 重复调用抛 `InvalidOperationException`。

- [ ] **步骤 4：运行 focused test**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter BuildFreezesBuilderMutationEntrypoints
```

预期：通过。

- [ ] **步骤 5：写 Dispose 后 mutating API 失败和诊断测试**

在 `TestHostTests` 中添加：

```csharp
[Fact]
public async Task DisposeStopsRuntimeFakesAndRejectsMutableUse()
{
    var host = TestHost.CreateBuilder().Build();

    await host.DisposeAsync();

    Assert.True(host.IsStopped);
    Assert.True(host.Diagnostics.Contains("AUCTEST001"));
    Assert.Throws<ObjectDisposedException>(() => host.Dispatcher.Post(() => { }));
    Assert.Throws<ObjectDisposedException>(() => host.Scheduler.Schedule(TimeSpan.Zero, () => { }));
}
```

- [ ] **步骤 6：运行失败测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter DisposeStopsRuntimeFakesAndRejectsMutableUse
```

预期：失败，原因是 dispatcher/scheduler 未随 host dispose，且没有 dispose diagnostic。

- [ ] **步骤 7：写最小实现**

`TestHost.Dispose` 和 `DisposeAsync` 必须幂等调用 `StopAsync()`，释放 dispatcher 和 scheduler，并写入 `AUCTEST001`。Dispose 后 dispatcher/scheduler mutation 抛 `ObjectDisposedException`。

- [ ] **步骤 8：运行 TestHost focused tests**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter TestHostTests
```

预期：`TestHostTests` 全部通过。

- [ ] **步骤 9：提交 TestHost 实现**

运行：

```bash
git add src/AtomUI.City.Testing/TestHostBuilder.cs src/AtomUI.City.Testing/TestHost.cs src/AtomUI.City.Testing/FakeUiDispatcher.cs src/AtomUI.City.Testing/DeterministicScheduler.cs tests/AtomUI.City.Testing.Tests/TestHostTests.cs
git commit -m "feat: harden testing host lifecycle"
```

预期：提交只包含 TestHost 生命周期、builder 冻结和必要 fake dispose 支撑。

### 任务 3：实现 FakeUiDispatcher 产品级线程和诊断语义

**文件：**

- 修改：`tests/AtomUI.City.Testing.Tests/FakeUiDispatcherTests.cs`
- 修改：`src/AtomUI.City.Testing/FakeUiDispatcher.cs`
- 修改：`src/AtomUI.City.Testing/FakeUiWorkItem.cs`

- [ ] **步骤 1：写 IUiDispatcher 和 FIFO 失败测试**

在 `FakeUiDispatcherTests` 中添加：

```csharp
[Fact]
public async Task ImplementsCoreDispatcherAndRunsInlineWhenAlreadyOnUiThread()
{
    var dispatcher = new FakeUiDispatcher();
    var calls = new List<string>();

    await dispatcher.InvokeAsync(() =>
    {
        Assert.True(dispatcher.CheckAccess());
        calls.Add("invoke");
    });
    await dispatcher.PostAsync(_ =>
    {
        calls.Add(dispatcher.CheckAccess() ? "post-ui" : "post-background");
        return ValueTask.CompletedTask;
    });

    Assert.Equal(["invoke"], calls);

    dispatcher.Drain();

    Assert.Equal(["invoke", "post-ui"], calls);
}
```

- [ ] **步骤 2：运行失败测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter ImplementsCoreDispatcherAndRunsInlineWhenAlreadyOnUiThread
```

预期：编译或运行失败，原因是 `FakeUiDispatcher` 未实现 Core dispatcher API。

- [ ] **步骤 3：写最小实现**

`FakeUiDispatcher` 实现 `AtomUI.City.Threading.IUiDispatcher`，`InvokeAsync` 立即在 fake UI thread marker 下执行，`PostAsync` 入队并在 `Drain()` 中以 FIFO 执行，`CheckAccess()` 只在 fake UI work 执行期间返回 true。

- [ ] **步骤 4：运行 focused test**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter ImplementsCoreDispatcherAndRunsInlineWhenAlreadyOnUiThread
```

预期：通过。

- [ ] **步骤 5：写异常和 pending 诊断失败测试**

在 `FakeUiDispatcherTests` 中添加：

```csharp
[Fact]
public void DrainRecordsWorkExceptionsAndContinuesRemainingWork()
{
    var diagnostics = new TestDiagnostics();
    var dispatcher = new FakeUiDispatcher(diagnostics);
    var calls = new List<string>();

    dispatcher.Post(() => throw new InvalidOperationException("boom"));
    dispatcher.Post(() => calls.Add("after"));

    dispatcher.Drain();

    Assert.Equal(["after"], calls);
    Assert.Equal(0, dispatcher.PendingCount);
    Assert.True(diagnostics.Contains("AUCTEST101"));
}
```

- [ ] **步骤 6：运行失败测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter DrainRecordsWorkExceptionsAndContinuesRemainingWork
```

预期：失败，原因是 work exception 没有被诊断记录并继续执行后续 work。

- [ ] **步骤 7：写最小实现**

`FakeUiDispatcher` 捕获 work exception，写入 `AUCTEST101` 诊断并继续 drain。`FakeUiWorkItem` 记录 Id、完成、取消和失败状态。

- [ ] **步骤 8：运行 FakeUiDispatcher focused tests**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter FakeUiDispatcherTests
```

预期：`FakeUiDispatcherTests` 全部通过。

- [ ] **步骤 9：提交 FakeUiDispatcher 实现**

运行：

```bash
git add src/AtomUI.City.Testing/FakeUiDispatcher.cs src/AtomUI.City.Testing/FakeUiWorkItem.cs tests/AtomUI.City.Testing.Tests/FakeUiDispatcherTests.cs docs/modules/testing/api-contracts.md
git commit -m "feat: enforce fake dispatcher contract"
```

预期：提交包含 dispatcher contract、work item 状态和诊断测试。

### 任务 4：实现 DeterministicScheduler 产品级虚拟时间语义

**文件：**

- 修改：`tests/AtomUI.City.Testing.Tests/SharedTestUtilitiesTests.cs`
- 修改：`src/AtomUI.City.Testing/DeterministicScheduler.cs`

- [ ] **步骤 1：写同一 due time 稳定排序失败测试**

在 `SharedTestUtilitiesTests` 中添加：

```csharp
[Fact]
public void DeterministicSchedulerRunsSameDueTimeWorkInScheduleOrder()
{
    var scheduler = new DeterministicScheduler();
    var calls = new List<string>();

    scheduler.Schedule(TimeSpan.FromSeconds(1), () => calls.Add("first"));
    scheduler.Schedule(TimeSpan.FromSeconds(1), () => calls.Add("second"));
    scheduler.Schedule(TimeSpan.Zero, () => calls.Add("zero"));

    scheduler.AdvanceBy(TimeSpan.FromSeconds(1));

    Assert.Equal(["zero", "first", "second"], calls);
}
```

- [ ] **步骤 2：运行失败测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter DeterministicSchedulerRunsSameDueTimeWorkInScheduleOrder
```

预期：失败或存在不稳定风险，原因是 PriorityQueue 只按 due time 排序，未把 enqueue order 纳入优先级。

- [ ] **步骤 3：写最小实现**

`DeterministicScheduler` 为每个 work 分配递增 id，并使用 `(DueAt, Id)` 作为 priority，确保同一 due time 按 schedule 顺序执行。

- [ ] **步骤 4：运行 focused test**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter DeterministicSchedulerRunsSameDueTimeWorkInScheduleOrder
```

预期：通过。

- [ ] **步骤 5：写异常、取消和 Dispose 后失败测试**

在 `SharedTestUtilitiesTests` 中添加：

```csharp
[Fact]
public void DeterministicSchedulerRecordsExceptionsAndRejectsDisposedSchedule()
{
    var diagnostics = new TestDiagnostics();
    var scheduler = new DeterministicScheduler(diagnostics);

    scheduler.Schedule(TimeSpan.Zero, () => throw new InvalidOperationException("boom"));
    scheduler.RunDueWork();

    Assert.True(diagnostics.Contains("AUCTEST201"));

    scheduler.Dispose();

    Assert.Throws<ObjectDisposedException>(() => scheduler.Schedule(TimeSpan.Zero, () => { }));
}
```

再添加取消测试：

```csharp
[Fact]
public void DeterministicSchedulerSkipsCanceledWork()
{
    var scheduler = new DeterministicScheduler();
    var wasCalled = false;

    var work = scheduler.Schedule(TimeSpan.Zero, () => wasCalled = true);
    work.Cancel();
    scheduler.RunDueWork();

    Assert.False(wasCalled);
    Assert.Equal(0, scheduler.ScheduledCount);
}
```

- [ ] **步骤 6：运行失败测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter "DeterministicSchedulerRecordsExceptionsAndRejectsDisposedSchedule|DeterministicSchedulerSkipsCanceledWork"
```

预期：编译或运行失败，原因是 `Schedule` 尚不返回可取消 work item，且没有异常诊断或 dispose guard。

- [ ] **步骤 7：写最小实现**

`Schedule` 返回可取消的 `ScheduledWorkItem` 公共句柄；work 执行异常写 `AUCTEST201` 并继续执行后续 due work；Dispose 后拒绝新 schedule。

- [ ] **步骤 8：运行 SharedTestUtilities focused tests**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter SharedTestUtilitiesTests
```

预期：`SharedTestUtilitiesTests` 全部通过。

- [ ] **步骤 9：提交 DeterministicScheduler 实现**

运行：

```bash
git add src/AtomUI.City.Testing/DeterministicScheduler.cs tests/AtomUI.City.Testing.Tests/SharedTestUtilitiesTests.cs docs/modules/testing/api-contracts.md
git commit -m "feat: harden deterministic scheduler"
```

预期：提交包含虚拟时间排序、取消、异常诊断和 dispose guard。

### 任务 5：更新 Testing 001-003 状态并运行阶段门禁

**文件：**

- 修改：`docs/modules/testing/features.md`
- 修改：`docs/modules/testing/implementation-plan.md`

- [ ] **步骤 1：更新状态矩阵**

将 `AUC-TESTING-001`、`AUC-TESTING-002`、`AUC-TESTING-003` 的状态更新为：

```text
Implemented
```

并在 `implementation-plan.md` 中把对应行更新为：

```text
Product Contract Tests: Verified
Implementation Gap: None for Phase 2 slice
Status: Implemented
```

- [ ] **步骤 2：运行 Testing 测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj
```

预期：Testing 测试全部通过。

- [ ] **步骤 3：运行 solution build 和全量测试**

运行：

```bash
dotnet build AtomUICity.slnx
dotnet test AtomUICity.slnx --no-build
```

预期：solution 构建和全量测试通过。

- [ ] **步骤 4：运行文档、public API 和 whitespace 门禁**

运行：

```bash
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
```

预期：全部通过。

- [ ] **步骤 5：提交状态更新**

运行：

```bash
git add docs/modules/testing/features.md docs/modules/testing/implementation-plan.md
git commit -m "docs: mark testing phase 2 slice verified"
```

预期：提交只包含 Testing 状态矩阵更新。

## 最终验收

完成本计划后必须重新运行：

```bash
dotnet build AtomUICity.slnx
dotnet test AtomUICity.slnx --no-build
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
```

验收结论必须基于本轮 fresh output，不得复用之前的输出。
