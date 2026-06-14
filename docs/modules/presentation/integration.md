# AtomUI.City.Presentation Integration

## 集成原则

- 必须说明依赖方向，不能只写“集成某模块”。
- 跨模块 contract 必须是 public API、manifest、generated output、options、diagnostics、MSBuild property、CLI envelope 或 template variable。
- 跨插件边界 contract 必须来自 Host 共享程序集。
- 集成失败必须有可测试的 Result、异常或诊断。

## 集成点

| Provider Module | Consumer Module | Contract | Direction | Lifecycle | Threading | Failure Behavior | Tests |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Hosting | AtomUI.City.Presentation | AtomUI.City.Presentation 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。 | Core -> Module 或执行边界 -> Module | 见 lifecycle.md | 见 threading.md | 启动/执行失败必须有 Result、异常或诊断。 | tests/AtomUI.City.Presentation.Tests |
| PluginSystem | AtomUI.City.Presentation | 插件可以通过 manifest 或 Host 共享 contract 贡献本模块能力；所有插件来源对象必须绑定 plugin owner 并可撤销。 | Plugin owner/manifest -> Module | load/enable/disable/unload 或 package/template/generator 边界 | 插件后台任务必须可取消 | 贡献撤销失败必须隔离。 | tests/AtomUI.City.Presentation.Tests |
| Testing | AtomUI.City.Presentation | Feature ID 和产品合同测试。 | Testing -> Module | 构造 -> 执行 -> 断言 -> 释放 | fake dispatcher / deterministic scheduler / snapshot | 测试失败阻止完成状态。 | tests/AtomUI.City.Presentation.Tests |

## 集成硬约束

- 所有 VisualTree 修改必须在 UI dispatcher 上执行。
- ViewLocator 默认使用 generated manifest 或显式注册。
- 插件 View、resource dictionary、localized binding 必须绑定 plugin lease。
- VisualTree 变化必须反馈到 ViewModel/State。

## 集成变更规则

新增跨模块集成时，必须同时更新 features、api-contracts、testing、compatibility，并在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中同步完成度。
