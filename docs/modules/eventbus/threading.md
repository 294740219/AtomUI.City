# AtomUI.City.EventBus Threading

## 线程模型范围

执行边界：Host runtime service。

## 模块线程硬约束

- Publish 不隐式切 UI 线程。
- handler 外部代码不能在总线内部锁内执行。
- 订阅必须返回可释放句柄并绑定 owner。

## 并发冲突策略

- handler 抛异常：记录 eventType、handlerType、operationId。
- subscription dispose 中 handler 正在执行：等待完成或按 cancellation 策略结束。
- 重复 dispose：幂等。

## UI 线程规则

- 非 Presentation 模块不得直接操作 Avalonia visual tree。
- 需要 UI 更新的结果必须通过 Presentation dispatcher、State 或 EventBus contract 间接到达 UI。
- Presentation 的 VisualTree 修改必须在 UI dispatcher 上执行。

## 后台任务和取消

- IO、网络、子进程、编译分析、插件扫描、缓存清理、streaming 和 handler 调用必须可取消。
- 取消后不得提交后续状态、缓存、事件、UI 或 generated output。
- 长生命周期后台任务必须绑定 owner；owner 释放时取消。

## 死锁规避

- 不在 UI 线程同步等待异步操作。
- 不在 lock 内调用用户 handler、插件代码、dispatcher、transport 或外部 process。
- 释放顺序从 leaf owner 到 parent owner。
