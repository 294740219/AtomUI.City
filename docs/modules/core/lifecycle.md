# AtomUI.City.Core Lifecycle

## 生命周期范围

执行边界：Host runtime kernel。

Core 定义 Host 本体，所有运行时模块都通过 IApplicationHostBuilder、LifecyclePipeline 和 ModuleBase 接入。

## 模块特有状态机

- Builder: Created -> Configuring -> Built -> Frozen
- Host: Created -> Starting -> Running -> Stopping -> Stopped -> Disposed
- Host failure: Starting -> Faulted -> Stopping -> Stopped 或 Disposed
- LifecycleScope: Running -> Stopping -> Stopped/Faulted -> Disposing -> Disposed
- Module: Declared -> ServicesConfigured -> Initialized -> Shutdown

## 生命周期流程

- CreateBuilder 收集 options、module registration、service actions 和 middleware。
- Build 创建 GenericHost、ApplicationContext、LifecycleScope root 和 diagnostics collector。
- StartAsync 依次执行 ApplicationStart、Generic Host、Application DI scope、module contribution/init/start，并在任一步失败时逆序补偿。
- StopAsync 阻止新操作进入，取消 scope tree，逆序执行 module shutdown，释放 Application DI scope，最后停止 Generic Host。
- DisposeAsync 从 leaf scope 到 root scope 释放；已提前释放的 child 会从 parent 脱离；释放异常进入 diagnostics 且不阻断同级清理。
- Host 和 Scope 的并发 Start/Stop/Dispose 调用合并到同一事务；调用方取消只取消等待，不中断已经开始的共享 Stop 事务。

## Host Shutdown / 执行结束行为

- Host 停止时阻止新操作进入。
- Created 状态收到 StopAsync 后直接进入 Stopped，之后禁止重新 Start。
- 取消未完成后台任务。
- 从 leaf owner 到 root owner 释放资源。
- 释放失败记录诊断并继续释放其他资源。

## 插件动态变更行为

- 插件来源对象必须绑定 plugin owner。
- 插件停用时先拒绝新贡献，再撤销现有贡献，最后释放对象。
- 跨插件 contract 类型必须来自 Host 共享程序集。

## 异常中断行为

- 模块依赖循环：Build 失败，诊断包含 cycle path。
- 重复 module id：Build 失败，诊断包含已有 module 和重复 module。
- lifecycle middleware 抛异常：当前 stage 失败，Host 进入 Faulted 并执行清理。
- Build 后继续注册服务：抛 InvalidOperationException 或返回失败 Result。
- UnavailableUiDispatcher 被执行：返回调度失败，不触碰 UI。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
