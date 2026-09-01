# AtomUI.City.Core Integration

## 集成原则

- 必须说明依赖方向，不能只写“集成某模块”。
- 跨模块 contract 必须是 public API、manifest、generated output、options、diagnostics、MSBuild property、CLI envelope 或 template variable。
- 跨插件边界 contract 必须来自 Host 共享程序集。
- 集成失败必须有可测试的 Result、异常或诊断。

## 集成点

| Provider Module | Consumer Module | Contract | Direction | Lifecycle | Threading | Failure Behavior | Tests |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Hosting | AtomUI.City.Core | Core 定义 Host 本体，所有运行时模块都通过 IApplicationHostBuilder、LifecyclePipeline 和 ModuleBase 接入。 | Core -> Module 或执行边界 -> Module | 见 lifecycle.md | 见 threading.md | 启动/执行失败必须有 Result、异常或诊断。 | tests/AtomUI.City.Core.Tests |
| Generators | AtomUI.City.Core | Generator 发出 `GeneratedModuleManifestAttribute` 和强类型 `IModuleRegistrar`；Core Build 消费 registrar 并建立 `ModuleCatalog`。 | Generated assembly metadata -> Core Build | Build 前生成，Build 阶段一次性读取 | Registrar 同步执行，不运行模块业务代码 | 非法 registrar 或 catalog 冲突导致 Build 失败并写 Host 诊断。 | Generator tests; GeneratedModuleCatalogTests |
| AtomUI.City.Core | Presentation | Core 提供 City Host Start/Stop、Microsoft `IHostApplicationLifetime` 集成点和 `IUiDispatcher` 抽象；Presentation 消费这些合同并协调 Avalonia runtime。 | Core contracts -> Presentation | UI runtime 启停边界 | UI 工作通过 IUiDispatcher | 适配失败由 Presentation 诊断，不改变 Core Host contract。 | Presentation integration tests |
| AtomUI.City.Core | PluginSystem | Core 提供 Module、Lifecycle、Diagnostics 和 Root Provider 冻结约束；PluginSystem 消费这些合同，并拥有插件发现、隔离服务容器、领域贡献撤销和卸载编排。 | Core contracts -> PluginSystem | load/enable/disable/unload | 插件后台任务必须可取消 | 插件隔离或撤销失败由 PluginSystem 诊断并隔离。 | PluginSystem tests |
| Testing | AtomUI.City.Core | Feature ID 和产品合同测试。 | Testing -> Module | 构造 -> 执行 -> 断言 -> 释放 | fake dispatcher / deterministic scheduler / snapshot | 测试失败阻止完成状态。 | tests/AtomUI.City.Core.Tests |

## 集成硬约束

- Core 不允许引用 Avalonia、AtomUI、Roslyn、CLI、Templates 或 Testing 生产程序集。
- ApplicationHostBuilder Build 后必须冻结服务注册入口。
- IUiDispatcher 只定义抽象，Core 不提交真实 UI work。

## 集成变更规则

新增跨模块集成时，必须同时更新 features、api-contracts、testing、compatibility，并在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中同步完成度。
