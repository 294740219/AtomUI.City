# AtomUI.City.State Integration

## 集成原则

- 必须说明依赖方向，不能只写“集成某模块”。
- 跨模块 contract 必须是 public API、manifest、generated output、options、diagnostics、MSBuild property、CLI envelope 或 template variable。
- 跨插件边界 contract 必须来自 Host 共享程序集。
- 集成失败必须有可测试的 Result、异常或诊断。

## 集成点

应用在服务配置阶段调用 `services.AddState()`。该入口注册共享的 `ApplicationStateRegistry`、读写接口、`IStateScopeAccessor` 和 `IStateFactory`；所有接口解析到同一套 Host runtime state services。

| Provider Module | Consumer Module | Contract | Direction | Lifecycle | Threading | Failure Behavior | Tests |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Hosting | AtomUI.City.State | AtomUI.City.State 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。 | Core -> Module 或执行边界 -> Module | 见 lifecycle.md | 见 threading.md | 启动/执行失败必须有 Result、异常或诊断。 | tests/AtomUI.City.State.Tests |
| PluginSystem | AtomUI.City.State | 插件可以通过 manifest 或 Host 共享 contract 贡献本模块能力；所有插件来源对象必须绑定 plugin owner 并可撤销。 | Plugin owner/manifest -> Module | load/enable/disable/unload 或 package/template/generator 边界 | 插件后台任务必须可取消 | 贡献撤销失败必须隔离。 | tests/AtomUI.City.State.Tests |
| Testing | AtomUI.City.State | Feature ID 和产品合同测试。 | Testing -> Module | 构造 -> 执行 -> 断言 -> 释放 | fake dispatcher / deterministic scheduler / snapshot | 测试失败阻止完成状态。 | tests/AtomUI.City.State.Tests |

## StateScope 与 Core 生命周期树的组合合同

`StateScope` 不是 Core `LifecycleScope` 的节点（不使用 `LifecycleScopeKind.State`）；二者通过**组合**绑定，绑定责任在创建方：

- 拥有 Core Scope 的创建方（Mvvm 激活流程、Routing 导航事务等）持有 StateScope，并把它登记进对应 Core Scope 的死亡路径——Mvvm 侧通过 `IActivationScope.Add(IDisposable)` 登记；Core 父 Scope 释放时由拥有方级联 Dispose StateScope，StateScope 再释放其全部订阅。
- `StateFactory.CreateScope` 同时把新 scope 登记进 ambient 父 StateScope（`IStateScopeAccessor.Current`），形成应用内的 StateScope 嵌套链。
- 未登记进任何 Core Scope 的 StateScope 由创建方自行持有并释放。
- 1.0 不提供 StateScope 到 Core Scope 的自动绑定或隐式挂接；该组合合同是 State 与 Mvvm/Routing 集成的既定边界。

## 集成硬约束

- 默认不隐式切 UI 线程。
- StateSnapshot 创建后不可变。
- ComputedState 不能形成循环依赖。
- 插件 state definition、subscription 和 snapshot provider 必须绑定插件 owner。

## 集成变更规则

新增跨模块集成时，必须同时更新 features、api-contracts、testing、compatibility，并在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中同步完成度。
