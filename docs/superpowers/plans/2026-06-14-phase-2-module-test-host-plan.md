# Phase 2 Module Test Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 `AUC-TESTING-004 Module Test Host` 的产品级测试闭环，使模块依赖图、生命周期、取消、失败诊断和释放语义能被后续模块测试复用。

**Architecture:** `ModuleTestHostBuilder` 负责收集测试模块、冻结 builder、验证模块依赖图并生成依赖序；`ModuleTestHost` 按依赖序执行 service、contribution 和 initialization 阶段，按反向依赖序 shutdown。所有 lifecycle failure 写入 `TestHost.Diagnostics`，并重新抛出原始异常。

**Tech Stack:** .NET `net10.0` Debug 目标、`AtomUI.City.Core` module attributes、`Microsoft.Extensions.DependencyInjection`、xUnit、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 范围决策

包含：

- `ModuleTestHostBuilder` Build 后冻结 mutation entrypoint。
- `[DependsOn]` required dependency 排序、missing dependency 失败、cycle 失败。
- `ModuleTestHost.InitializeAsync(CancellationToken)` 和 `ShutdownAsync(CancellationToken)` 观察 token。
- lifecycle stage 失败写 `AUCTEST301`，shutdown stage 失败写 `AUCTEST302`。
- Dispose 时自动 shutdown，重复 shutdown/dispose 幂等。

不包含：

- 动态插件 module 加载。
- Source Generator manifest 消费。
- Contribution registry 的 owner revoke 完整断言。
- `AUC-TESTING-005` 及后续 Feature。

## 文件地图

实现文件：

- `src/AtomUI.City.Testing/ModuleTestHostBuilder.cs`
- `src/AtomUI.City.Testing/ModuleTestHost.cs`
- `src/AtomUI.City.Testing/ModuleTestRecord.cs`

测试文件：

- `tests/AtomUI.City.Testing.Tests/ModuleTestHostTests.cs`

文档文件：

- `docs/modules/testing/api-contracts.md`
- `docs/modules/testing/features.md`
- `docs/modules/testing/implementation-plan.md`
- `docs/superpowers/plans/2026-06-14-phase-2-module-test-host-plan.md`

## 执行规则

- 每个行为先写失败测试，再写最小实现。
- 新增或修改 public API 前先更新 `docs/modules/testing/api-contracts.md`。
- 每个任务完成后运行 `dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter ModuleTestHostTests`。
- 完成状态更新前必须运行 Testing 测试、solution build、全量测试、文档门禁、public API 门禁和 `git diff --check`。

## 任务

### 任务 1：冻结 Module Test Host 方法合同

**文件：**

- 修改：`docs/modules/testing/api-contracts.md`
- 新增：`docs/superpowers/plans/2026-06-14-phase-2-module-test-host-plan.md`

- [ ] **步骤 1：补齐方法合同**

在 `docs/modules/testing/api-contracts.md` 中补齐：

```text
ModuleTestHostBuilder.UseModule
ModuleTestHostBuilder.UseHostProperty
ModuleTestHostBuilder.Build
ModuleTestHost.InitializeAsync
ModuleTestHost.ShutdownAsync
ModuleTestHost.Dispose
ModuleTestHost.DisposeAsync
```

每个合同必须包含用途、参数、返回值、空值规则、取消、异常或失败结果、幂等、并发、副作用、诊断和测试文件。

- [ ] **步骤 2：运行文档检查**

运行：

```bash
bash engineering/check-docs.sh
```

预期：通过。

- [ ] **步骤 3：提交合同冻结**

运行：

```bash
git add docs/modules/testing/api-contracts.md docs/superpowers/plans/2026-06-14-phase-2-module-test-host-plan.md
git commit -m "docs: freeze module test host contracts"
```

### 任务 2：实现 builder 冻结和模块依赖图排序

**文件：**

- 修改：`tests/AtomUI.City.Testing.Tests/ModuleTestHostTests.cs`
- 修改：`src/AtomUI.City.Testing/ModuleTestHostBuilder.cs`

- [ ] **步骤 1：写失败测试**

新增测试：

```csharp
[Fact]
public async Task InitializeAsyncRunsModulesInDependencyOrder()
{
    var calls = new List<string>();
    await using var host = ModuleTestHost
        .CreateBuilder()
        .UseModule("App", new AppModule(calls))
        .UseModule("Core", new CoreModule(calls))
        .Build();

    await host.InitializeAsync();

    Assert.True(calls.IndexOf("Core:ConfigureServices") < calls.IndexOf("App:ConfigureServices"));
}
```

再新增 builder 冻结测试：

```csharp
[Fact]
public void BuildFreezesModuleTestHostBuilder()
{
    var builder = ModuleTestHost.CreateBuilder()
        .UseModule("Core", new CoreModule([]));

    using var host = builder.Build();

    Assert.Throws<InvalidOperationException>(() => builder.UseModule("Other", new CoreModule([])));
    Assert.Throws<InvalidOperationException>(() => builder.UseHostProperty("next", "value"));
    Assert.Throws<InvalidOperationException>(() => builder.Build());
}
```

- [ ] **步骤 2：运行失败测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter "InitializeAsyncRunsModulesInDependencyOrder|BuildFreezesModuleTestHostBuilder"
```

预期：依赖序测试失败，builder 冻结测试失败。

- [ ] **步骤 3：写最小实现**

`ModuleTestHostBuilder.Build` 解析 module instance 的 concrete type，按 `[DependsOn]` required dependency 做 DFS 排序。Build 成功后冻结 builder mutation entrypoint。

- [ ] **步骤 4：运行 focused tests**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter "InitializeAsyncRunsModulesInDependencyOrder|BuildFreezesModuleTestHostBuilder"
```

预期：通过。

### 任务 3：实现 graph failure 和 lifecycle diagnostics

**文件：**

- 修改：`tests/AtomUI.City.Testing.Tests/ModuleTestHostTests.cs`
- 修改：`src/AtomUI.City.Testing/ModuleTestHostBuilder.cs`
- 修改：`src/AtomUI.City.Testing/ModuleTestHost.cs`

- [ ] **步骤 1：写 graph failure 测试**

新增测试 missing dependency 和 cycle：

```csharp
[Fact]
public void BuildFailsWhenRequiredDependencyIsMissing()
{
    var builder = ModuleTestHost.CreateBuilder()
        .UseModule("App", new DependsOnMissingModule());

    var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

    Assert.Contains(nameof(DependsOnMissingModule), exception.Message, StringComparison.Ordinal);
    Assert.Contains(nameof(MissingModule), exception.Message, StringComparison.Ordinal);
}
```

- [ ] **步骤 2：写 lifecycle diagnostics 失败测试**

新增测试：

```csharp
[Fact]
public async Task InitializeAsyncRecordsDiagnosticsWhenModuleStageFails()
{
    await using var host = ModuleTestHost.CreateBuilder()
        .UseModule("Failing", new FailingInitializationModule())
        .Build();

    await Assert.ThrowsAsync<InvalidOperationException>(async () => await host.InitializeAsync());

    Assert.True(host.Host.Diagnostics.Contains("AUCTEST301"));
}
```

- [ ] **步骤 3：运行失败测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter "BuildFailsWhenRequiredDependencyIsMissing|InitializeAsyncRecordsDiagnosticsWhenModuleStageFails"
```

预期：缺失依赖失败信息或 diagnostics 缺失。

- [ ] **步骤 4：写最小实现**

Build 阶段对 missing dependency 和 cycle 抛 `InvalidOperationException`。`ModuleTestHost` 每个 lifecycle stage 通过统一 helper 调用，捕获异常时写 `AUCTEST301` 并重新抛出。

- [ ] **步骤 5：运行 focused tests**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter ModuleTestHostTests
```

预期：全部通过。

### 任务 4：实现 cancellation 和状态更新

**文件：**

- 修改：`tests/AtomUI.City.Testing.Tests/ModuleTestHostTests.cs`
- 修改：`src/AtomUI.City.Testing/ModuleTestHost.cs`
- 修改：`docs/modules/testing/features.md`
- 修改：`docs/modules/testing/implementation-plan.md`

- [ ] **步骤 1：写取消测试**

新增测试：

```csharp
[Fact]
public async Task InitializeAsyncPassesCancellationTokenToModules()
{
    using var cancellation = new CancellationTokenSource();
    await cancellation.CancelAsync();
    await using var host = ModuleTestHost.CreateBuilder()
        .UseModule("Cancellable", new CancellationObservingModule())
        .Build();

    await Assert.ThrowsAsync<OperationCanceledException>(async () => await host.InitializeAsync(cancellation.Token));
}
```

- [ ] **步骤 2：运行失败测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter InitializeAsyncPassesCancellationTokenToModules
```

预期：编译或运行失败，原因是 `InitializeAsync` 未暴露 token 或未传递 token。

- [ ] **步骤 3：写最小实现**

`InitializeAsync` 和 `ShutdownAsync` 增加可选 `CancellationToken` 参数，并传递给所有 module lifecycle API。

- [ ] **步骤 4：更新状态矩阵**

将 `AUC-TESTING-004` 状态改为 `Implemented`，`Product Contract Tests` 改为 `Verified`，`Implementation Gap` 改为 `None for Phase 2 slice`。

- [ ] **步骤 5：运行最终门禁**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj
dotnet build AtomUICity.slnx
dotnet test AtomUICity.slnx --no-build
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
```

预期：全部通过。

- [ ] **步骤 6：提交实现和状态**

运行：

```bash
git add src/AtomUI.City.Testing/ModuleTestHostBuilder.cs src/AtomUI.City.Testing/ModuleTestHost.cs tests/AtomUI.City.Testing.Tests/ModuleTestHostTests.cs docs/modules/testing/features.md docs/modules/testing/implementation-plan.md
git commit -m "feat: harden module test host"
```
