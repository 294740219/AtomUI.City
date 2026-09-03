# AtomUI.City.EventBus Threading

## 线程模型范围

执行边界：Host runtime service。

## 模块线程硬约束

- Publish 不隐式切 UI 线程。
- handler 外部代码不能在总线内部锁内执行。
- 订阅必须返回可释放句柄并绑定 owner。

## 并发冲突策略

- handler 抛异常：记录 eventType、handlerType、operationId。
- EventBus accepting 状态、owner Running 状态、owner token registration、subscription 状态和 snapshot 提交属于一个原子注册协议。
- owner/EventBus stop 与 Subscribe 竞争时，新订阅要么完整进入 Active 后立即汇入同一个 Quiescing 事务，要么在提交前失败；不得留下半注册订阅。
- `Dispose()`、`StopAsync()`、`DisposeAsync()` 和 owner cancellation 只发布一个终止 Task；重复调用共享该事务。
- Quiescing 与从 snapshot 移除构成新 delivery barrier；已有 delivery 进入 Draining 并由异步停止入口等待。

## UI 线程规则

- 非 Presentation 模块不得直接操作 Avalonia visual tree。
- 需要 UI 更新的结果必须通过 Presentation dispatcher、State 或 EventBus contract 间接到达 UI。
- Presentation 的 VisualTree 修改必须在 UI dispatcher 上执行。

## 后台任务和取消

- IO、网络、子进程、编译分析、插件扫描、缓存清理、streaming 和 handler 调用必须可取消。
- 取消后不得提交后续状态、缓存、事件、UI 或 generated output。
- 长生命周期后台任务必须绑定 owner；owner 释放时取消。
- 每次 delivery 的 token 组合 publisher、owner/subscription 和 EventBus shutdown；owner cancellation 必须到达正在执行的 handler。
- Cancellation callback 只能建立 Quiescing barrier、触发取消并发布终止事务，不能同步等待 handler。
- StopAsync 的调用方 token 只取消当前等待，不能撤销已经发布的终止事务。

## 死锁规避

- 不在 UI 线程同步等待异步操作。
- 不在 lock 内调用用户 handler、插件代码、dispatcher、transport 或外部 process。
- 不在 `LifecycleScope` cancellation callback 中同步 drain subscription。
- 释放顺序从 leaf owner 到 parent owner。
