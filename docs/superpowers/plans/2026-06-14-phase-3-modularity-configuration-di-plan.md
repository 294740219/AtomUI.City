# Phase 3 Modularity、Configuration 和 DI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付 Phase 3 的产品级 Core 闭环，让模块合同、模块来源、PreConfigure 配置底座和服务注册阶段边界可测试、可诊断、可继续支撑后续 State、Routing、PluginSystem。

**Architecture:** 本阶段继续约束在 `AtomUI.City.Core`，不引入 Source Generator、插件程序集加载或运行时程序集扫描。模块系统负责稳定拓扑顺序和阶段调度；配置底座通过 `ServiceConfigurationContext` 暴露同步 `PreConfigure<TOptions>`；DI 阶段边界通过可冻结的 `ModuleServiceCollection` 防止模块持有配置阶段对象并在 Host 构建后继续修改。

**Tech Stack:** .NET `net10.0` Debug 目标、xUnit、`Microsoft.Extensions.DependencyInjection`、`engineering/check-docs.sh`、`engineering/check-public-api.sh`。

---

## 任务

- [x] 更新 `docs/modules/core/api-contracts.md`，记录 Phase 3 新增/硬化 API：`ModuleOrigin`、`ModuleDescriptor.Origin`、`ModuleDescriptor.PluginId`、`ServiceConfigurationContext.PreConfigure<TOptions>`、`ServiceConfigurationContext.ExecutePreConfigure<TOptions>`、`ModuleServiceCollection` 阶段冻结。
- [x] 写模块合同失败测试：`ModuleBase` async lifecycle 方法必须拒绝 null context、在进入同步便利方法前观察已取消 token；`ModuleDescriptor` 和 module graph 必须拒绝非 `IModule` 类型。
- [x] 实现模块合同硬化的最小代码。
- [x] 写模块来源失败测试：默认 descriptor 来源为 `Application`；插件 descriptor 必须带 `PluginId`；Application descriptor 不允许带 `PluginId`；属性快照不可变。
- [x] 实现 `ModuleOrigin`、descriptor 来源字段和校验。
- [x] 写 PreConfigure 失败测试：多个模块按拓扑顺序注册 `PreConfigure<TOptions>` action，后续 `ConfigureServices` 执行时按顺序应用；action/options 为 null 必须失败。
- [x] 实现 `PreConfigureActionStore` 以及 `ServiceConfigurationContext` 上的 `PreConfigure<TOptions>` / `ExecutePreConfigure<TOptions>`。
- [x] 写服务注册阶段边界失败测试：模块捕获 `ModuleServiceCollection` 后，Host Build 结束继续 mutating 必须失败。
- [x] 实现 `ModuleServiceCollection.Freeze()` 并在 `ModuleRegistry.ConfigureServicesAsync` 完成后冻结。
- [x] 同步 `docs/modules/core/features.md` 和 `docs/modules/core/implementation-plan.md`，把 Phase 3 覆盖的 Core 功能状态更新为已验证，并保持中文描述。
- [ ] 运行完整门禁：`dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj`、`dotnet build AtomUICity.slnx`、`dotnet test AtomUICity.slnx --no-build`、`bash engineering/check-docs.sh`、`bash engineering/check-public-api.sh`、`git diff --check`。
