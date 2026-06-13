# AtomUI.City.Presentation Lifecycle

## 生命周期范围

执行边界：Avalonia/AtomUI runtime bridge。

AtomUI.City.Presentation 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- PresentationRuntime: Created -> Starting -> Running -> Stopping -> Stopped -> Disposed
- RouteOutlet: Empty -> Preparing -> Committing -> Committed 或 Failed

## 生命周期流程

- Routing 输出 ViewModelTargetDescriptor。
- ViewLocator 找到 ViewDescriptor。
- ViewFactory 创建 View。
- RouteOutlet 在 UI dispatcher 上提交 View。

## Host Shutdown / 执行结束行为

- Host 停止时阻止新操作进入。
- 取消未完成后台任务。
- 从 leaf owner 到 root owner 释放资源。
- 释放失败记录诊断并继续释放其他资源。

## 插件动态变更行为

- 插件来源对象必须绑定 plugin owner。
- 插件停用时先拒绝新贡献，再撤销现有贡献，最后释放对象。
- 跨插件 contract 类型必须来自 Host 共享程序集。

## 异常中断行为

- View 未注册：返回失败并诊断。
- 非 UI 线程提交：拒绝并诊断。
- View 创建失败：不替换现有 outlet。
- 插件卸载 active view：detach 并撤销资源。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
