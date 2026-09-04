# AtomUI.City.EventBus Threading

## 线程模型范围

执行边界：Host runtime service。

## 模块线程硬约束

- Publish 不隐式切 UI 线程。
- 所有 publication 先进入 AUC-EVENTBUS-007 的 channel runtime，因此 `Current` 表示当前 delivery worker，不表示 publisher 调用线程。
- Channel worker 和默认后台 scheduler 不继承创建方 `ExecutionContext`；不得把首次 publisher 的 AsyncLocal/安全上下文泄漏给后续事件。
- handler 外部代码不能在总线内部锁内执行。
- 订阅必须返回可释放句柄并绑定 owner。

## 并发冲突策略

- handler 抛异常：记录 eventType、handlerType、operationId。
- EventBus accepting 状态、owner Running 状态、owner token registration、subscription 状态和 snapshot 提交属于一个原子注册协议。
- owner/EventBus stop 与 Subscribe 竞争时，新订阅要么完整进入 Active 后立即汇入同一个 Quiescing 事务，要么在提交前失败；不得留下半注册订阅。
- `Dispose()`、`StopAsync()`、`DisposeAsync()` 和 owner cancellation 只发布一个终止 Task；重复调用共享该事务。
- Quiescing 与从 snapshot 移除构成新 delivery barrier；已有 delivery 进入 Draining 并由异步停止入口等待。
- Plugin contribution 的 subscriptions、active operations、Scope 与 private runtime 共享一个总 drain deadline；不得为每个子步骤重新开始 timeout。deadline 超时完成唯一公开终止 Task，迟到清理由独立观察任务接管，异常不得成为未观察 Task。
- Plugin contribution lock 内只允许状态、配额、pending/active 计数与集合快照操作；Registry 查询、EventBus Subscribe/Publish/Post、Diagnostics、Scope、用户 callback 和 controller removal 必须在锁外。Subscribe 以 pending reservation + 二次状态复核提交；Stop 等待 pending registration 的提交或完整回滚，禁止半注册。
- EventBus controller 总锁内不得调用 contribution lease Dispose/Stop；创建提交失败只在锁内记录决定，实际回滚在锁外执行。

## UI 线程规则

- 非 Presentation 模块不得直接操作 Avalonia visual tree。
- 需要 UI 更新的结果必须通过 Presentation dispatcher、State 或 EventBus contract 间接到达 UI。
- Presentation 的 VisualTree 修改必须在 UI dispatcher 上执行。
- `UiThread + Post` 即使 dispatcher 的 `PostAsync` 只报告“已入队”，`PublishAsync` 仍必须等待 callback 真正完成；`InlineIfAllowed` 只有在 `CheckAccess()` 为 true 时才允许 inline。

## 后台任务和取消

- IO、网络、子进程、编译分析、插件扫描、缓存清理、streaming 和 handler 调用必须可取消。
- 取消后不得提交后续状态、缓存、事件、UI 或 generated output。
- 长生命周期后台任务必须绑定 owner；owner 释放时取消。
- 每次 delivery 的 token 组合 publisher、owner/subscription 和 EventBus shutdown；owner cancellation 必须到达正在执行的 handler。
- Cancellation callback 只能建立 Quiescing barrier、触发取消并发布终止事务，不能同步等待 handler。
- StopAsync 的调用方 token 只取消当前等待，不能撤销已经发布的终止事务。
- Handler timeout 只结束 publication 对本次 delivery 的等待并触发 handler token；忽略 token 的 handler 继续计入 subscription in-flight，终止事务仍需等待其真实退出。
- 单次 publication 的独立 delivery fan-out 必须服从 `EventBusDispatchOptions` 上限，不能按订阅数量无界创建后台工作。

## 死锁规避

- 不在 UI 线程同步等待异步操作。
- 不在 lock 内调用用户 handler、插件代码、dispatcher、transport 或外部 process。
- 不在 `LifecycleScope` cancellation callback 中同步 drain subscription。
- 释放顺序从 leaf owner 到 parent owner。
