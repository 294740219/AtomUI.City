# AtomUI.City.EventBus Lifecycle

## 生命周期范围

执行边界：Host runtime service。

AtomUI.City.EventBus 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- EventBus: Active -> Quiescing -> Draining -> Disposed；终止失败由共享 DisposeAsync 事务传播。单独 `AddEventBus` 构造完成即 Active；City Host-managed runtime 在 Core pre-application-initialization 之前保持 closed gate，成功绑定真实 ApplicationScope 后才进入 Active，该 gate 不是额外公开状态。
- Subscription: Created -> Active -> Quiescing -> Draining -> Disposed；终止或清理失败进入 Faulted
- Publish: Created -> Dispatching -> Completed 或 Failed 或 Cancelled
- Contract registry: MutableDuringConfiguration -> FrozenAtRuntime；插件动态贡献走 snapshot 替换

## 生命周期流程

- 动态 Subscribe 验证 contract、记录唯一 `LifecycleScope` owner 并创建 subscription id；不提供 ownerless 动态订阅。
- Subscribe 提交必须原子复核 EventBus accepting 状态、owner Running 状态和 owner token；与 stop 竞争时不得留下 Active 或半注册 subscription。
- Publish 创建 EventContext，选定 dispatch policy，按稳定顺序调用 handler。
- handler 失败按 EventErrorPolicy 继续、停止或聚合错误。
- EventBus Dispose 幂等并快速建立 Quiescing barrier，阻止新的 publish、post 和 subscribe；DisposeAsync 等待同一事务取消 queued/in-flight publication、结束 channel worker 并释放 active subscriptions。
- owner cancellation 立即把该 owner 的 subscription 移出新 snapshot 并触发 handler cancellation，不影响其他 owner 对同一事件的订阅。
- Subscription `Dispose()` 只执行快速 Quiescing，不等待异步 handler；`StopAsync()`、`DisposeAsync()` 和 owner cancellation 共享唯一终止事务。
- 调用方 token 或 shutdown deadline 只取消等待；后台终止继续到 Disposed 或 Faulted，不产生永久 StopTimedOut 状态。

## Host Shutdown / 执行结束行为

- Host 停止时阻止新操作进入。
- 取消未完成后台任务。
- EventBus Host controller 等待 ApplicationScope 所有 subscription 的终止事务。
- 从 leaf owner 到 root owner 释放资源。
- 释放失败记录诊断并继续释放其他资源。

## 插件动态变更行为

- 插件来源对象必须绑定 plugin owner。
- 插件停用时先拒绝新贡献，再撤销现有贡献，最后释放对象。
- PluginSystem 在卸载插件资源前等待 EventBus 领域 ContributionLease 持有的 subscriptions 完成 drain。
- ContributionLease 使用配额中的单一总 DrainTimeout；超时后进入 Faulted 并结束公开等待，但迟到清理继续受观察，真实清理完成前 PluginId 不得复用。
- 跨插件 contract 类型必须来自 Host 共享程序集。

## 异常中断行为

- 未登记跨边界 contract：拒绝 publish 并诊断。
- handler 抛异常：记录 eventType、handlerType、operationId。
- subscription dispose 中 handler 正在执行：等待完成或按 cancellation 策略结束。
- EventBus 重复 dispose：幂等。
- Subscription 重复 dispose：幂等。
- Core `LifecycleScope.StopAsync()` 当前不承诺等待 EventBus 外部资源 drain；要求确定性 teardown 的 Window/Route 协调器必须显式等待对应 `IEventSubscription.StopAsync()`。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
