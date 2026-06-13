# Phase 2 Plugin Test Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 `AUC-TESTING-005 Plugin Test Host` 的产品级测试闭环，使插件安装、激活、停用、卸载、贡献 owner revoke、取消和释放行为可由 Testing 包稳定断言。

**Architecture:** `PluginTestHostBuilder` 只负责声明测试插件包并冻结配置；`PluginTestHost` 在 `TestHost` 临时目录中创建 package manifest，维护插件 state record 和 contribution ownership。卸载或释放 Host 时必须撤销贡献并记录诊断，测试不加载真实插件程序集。

**Tech Stack:** .NET `net10.0` Debug 目标、`AtomUI.City.Testing` fake host、xUnit、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 范围决策

包含：

- `PluginTestHostBuilder` Build 后冻结 mutation entrypoint。
- duplicate plugin id 在 Build 时失败。
- `InstallAsync`、`ActivateAsync`、`DeactivateAsync`、`UnloadAsync` 支持可选 `CancellationToken`。
- `RegisterContribution` 记录插件贡献 owner。
- `UnloadAsync` 和 Dispose 自动 revoke 未撤销贡献，写 `AUCTEST401`。
- Dispose 后 mutating API 抛 `ObjectDisposedException`。

不包含：

- 真实插件程序集加载、AssemblyLoadContext unload 验证。
- 插件依赖解析和版本冲突完整实现。
- Manifest schema 完整校验。
- `AUC-TESTING-006` 及后续 Feature。

## 文件地图

实现文件：

- `src/AtomUI.City.Testing/PluginTestHostBuilder.cs`
- `src/AtomUI.City.Testing/PluginTestHost.cs`
- `src/AtomUI.City.Testing/PluginTestRecord.cs`

测试文件：

- `tests/AtomUI.City.Testing.Tests/PluginTestHostTests.cs`

文档文件：

- `docs/modules/testing/api-contracts.md`
- `docs/modules/testing/features.md`
- `docs/modules/testing/implementation-plan.md`
- `docs/superpowers/plans/2026-06-14-phase-2-plugin-test-host-plan.md`

## 任务

### 任务 1：冻结 Plugin Test Host 方法合同

**文件：**

- 修改：`docs/modules/testing/api-contracts.md`
- 新增：`docs/superpowers/plans/2026-06-14-phase-2-plugin-test-host-plan.md`

- [ ] **步骤 1：补齐方法合同**

在 `docs/modules/testing/api-contracts.md` 中补齐：

```text
PluginTestHostBuilder.UsePlugin
PluginTestHostBuilder.Build
PluginTestHost.InstallAsync
PluginTestHost.ActivateAsync
PluginTestHost.DeactivateAsync
PluginTestHost.RegisterContribution
PluginTestHost.UnloadAsync
PluginTestHost.Dispose
PluginTestHost.DisposeAsync
```

- [ ] **步骤 2：运行文档检查**

运行：

```bash
bash engineering/check-docs.sh
```

- [ ] **步骤 3：提交合同冻结**

运行：

```bash
git add docs/modules/testing/api-contracts.md docs/superpowers/plans/2026-06-14-phase-2-plugin-test-host-plan.md
git commit -m "docs: freeze plugin test host contracts"
```

### 任务 2：实现 builder 冻结和 duplicate id 失败

**文件：**

- 修改：`tests/AtomUI.City.Testing.Tests/PluginTestHostTests.cs`
- 修改：`src/AtomUI.City.Testing/PluginTestHostBuilder.cs`

- [ ] **步骤 1：写失败测试**

新增：

```csharp
[Fact]
public void BuildFreezesPluginTestHostBuilder()
{
    var builder = PluginTestHost.CreateBuilder()
        .UsePlugin("Sample.Plugin", "1.0.0");

    using var host = builder.Build();

    Assert.Throws<InvalidOperationException>(() => builder.UsePlugin("Other.Plugin", "1.0.0"));
    Assert.Throws<InvalidOperationException>(() => builder.Build());
}
```

新增：

```csharp
[Fact]
public void BuildRejectsDuplicatePluginIds()
{
    var builder = PluginTestHost.CreateBuilder()
        .UsePlugin("Sample.Plugin", "1.0.0")
        .UsePlugin("Sample.Plugin", "2.0.0");

    var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

    Assert.Contains("Sample.Plugin", exception.Message, StringComparison.Ordinal);
}
```

- [ ] **步骤 2：运行失败测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter "BuildFreezesPluginTestHostBuilder|BuildRejectsDuplicatePluginIds"
```

- [ ] **步骤 3：写最小实现**

Build 成功后冻结 builder；duplicate id 在 Build 前校验并抛 `InvalidOperationException`。

### 任务 3：实现贡献 revoke、取消和 Dispose guard

**文件：**

- 修改：`tests/AtomUI.City.Testing.Tests/PluginTestHostTests.cs`
- 修改：`src/AtomUI.City.Testing/PluginTestHost.cs`
- 修改：`src/AtomUI.City.Testing/PluginTestRecord.cs`

- [ ] **步骤 1：写贡献 revoke 失败测试**

新增：

```csharp
[Fact]
public async Task UnloadAsyncRevokesPluginContributionsAndRecordsDiagnostics()
{
    await using var host = PluginTestHost.CreateBuilder()
        .UsePlugin("Sample.Plugin", "1.0.0")
        .Build();

    await host.InstallAsync("Sample.Plugin");
    await host.ActivateAsync("Sample.Plugin");
    var record = host.RegisterContribution("Sample.Plugin", "main-menu");

    await host.UnloadAsync("Sample.Plugin");

    Assert.Empty(record.Contributions);
    Assert.Equal(1, record.RevokedContributionCount);
    Assert.True(host.Host.Diagnostics.Contains("AUCTEST401"));
}
```

- [ ] **步骤 2：写取消和 Dispose guard 失败测试**

新增：

```csharp
[Fact]
public async Task PluginLifecycleMethodsObserveCancellationToken()
{
    using var cancellation = new CancellationTokenSource();
    await cancellation.CancelAsync();
    await using var host = PluginTestHost.CreateBuilder()
        .UsePlugin("Sample.Plugin", "1.0.0")
        .Build();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(
        async () => await host.InstallAsync("Sample.Plugin", cancellation.Token));
}
```

新增：

```csharp
[Fact]
public async Task DisposeUnloadsPluginsAndRejectsMutableUse()
{
    var host = PluginTestHost.CreateBuilder()
        .UsePlugin("Sample.Plugin", "1.0.0")
        .Build();

    var record = await host.InstallAsync("Sample.Plugin");
    await host.ActivateAsync("Sample.Plugin");
    host.RegisterContribution("Sample.Plugin", "main-menu");
    await host.DisposeAsync();

    Assert.Equal(PluginTestState.Unloaded, record.State);
    Assert.Empty(record.Contributions);
    Assert.Throws<ObjectDisposedException>(() => host.RegisterContribution("Sample.Plugin", "after"));
}
```

- [ ] **步骤 3：运行失败测试**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj --filter "UnloadAsyncRevokesPluginContributionsAndRecordsDiagnostics|PluginLifecycleMethodsObserveCancellationToken|DisposeUnloadsPluginsAndRejectsMutableUse"
```

- [ ] **步骤 4：写最小实现**

`PluginTestRecord` 暴露 contribution snapshot 和 revoke counter。`PluginTestHost` 在 unload/dispose 时撤销贡献，写 `AUCTEST401`，并对所有 mutating API 增加 disposed guard 和 token 观察。

### 任务 4：更新状态并运行门禁

**文件：**

- 修改：`docs/modules/testing/features.md`
- 修改：`docs/modules/testing/implementation-plan.md`

- [ ] **步骤 1：更新状态矩阵**

将 `AUC-TESTING-005` 标记为 `Implemented`、`Verified` 和 `None for Phase 2 slice`。

- [ ] **步骤 2：运行最终门禁**

运行：

```bash
dotnet test tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj
dotnet build AtomUICity.slnx
dotnet test AtomUICity.slnx --no-build
bash engineering/check-docs.sh
bash engineering/check-public-api.sh
git diff --check
```

- [ ] **步骤 3：提交实现和状态**

运行：

```bash
git add src/AtomUI.City.Testing/PluginTestHostBuilder.cs src/AtomUI.City.Testing/PluginTestHost.cs src/AtomUI.City.Testing/PluginTestRecord.cs tests/AtomUI.City.Testing.Tests/PluginTestHostTests.cs docs/modules/testing/features.md docs/modules/testing/implementation-plan.md
git commit -m "feat: harden plugin test host"
```
