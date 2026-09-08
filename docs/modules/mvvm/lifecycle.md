# AtomUI.City.Mvvm Lifecycle

## 生命周期范围

执行边界：ViewModel programming model。

AtomUI.City.Mvvm 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- ViewModel: Constructed -> Activating -> Active -> Deactivating -> Deactivated -> Disposed
- Command: Idle -> Running -> Completed 或 Failed 或 Canceled 或 Rejected 或 Rejected
- OperationScope: Running -> Completed、Failed、Canceled 或 Rejected；首次终态胜出，Dispose 后保持只读结果。

## 生命周期流程

- Presentation 创建或解析 ViewModel。
- ActivationScope 调用 IActivatable。
- CommandFactory 创建 command。
- Interaction 由 handler 返回结果。

## Host Shutdown / 执行结束行为

- Host 停止时阻止新操作进入。
- 取消未完成后台任务；OperationScope 必须先提交 Canceled 状态，再通知 operation token callback。
- 从 leaf owner 到 root owner 释放资源。
- 释放失败记录诊断并继续释放其他资源。

## 插件动态变更行为

- 本模块不持有运行时插件对象。
- 只通过 manifest、package、template、CLI 或 generator 支持插件开发和检查。

## 异常中断行为

- CanDeactivate 拒绝：导航或关闭中止。
- Command 抛异常：OperationResult Failed。
- Interaction 无 handler：返回 Failed。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
