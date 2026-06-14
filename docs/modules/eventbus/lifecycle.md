# AtomUI.City.EventBus Lifecycle

## 生命周期范围

执行边界：Host runtime service。

AtomUI.City.EventBus 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- EventBus: Active -> Disposed
- Subscription: Created -> Active -> Disposing -> Disposed
- Publish: Created -> Dispatching -> Completed 或 Failed 或 Cancelled
- Contract registry: MutableDuringConfiguration -> FrozenAtRuntime；插件动态贡献走 snapshot 替换

## 生命周期流程

- Subscribe 验证 contract、记录 owner、创建 subscription id。
- Publish 创建 EventContext，选定 dispatch policy，按稳定顺序调用 handler。
- handler 失败按 EventErrorPolicy 继续、停止或聚合错误。
- EventBus Dispose 幂等，释放 active subscriptions，并阻止新的 publish、post 和 subscribe。
- owner dispose 或插件 unload 释放 subscription。

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

- 未登记跨边界 contract：拒绝 publish 并诊断。
- handler 抛异常：记录 eventType、handlerType、operationId。
- subscription dispose 中 handler 正在执行：等待完成或按 cancellation 策略结束。
- EventBus 重复 dispose：幂等。
- Subscription 重复 dispose：幂等。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
