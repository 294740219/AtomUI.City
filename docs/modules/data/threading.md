# AtomUI.City.Data Threading

## 线程模型范围

执行边界：Host runtime data pipeline。

## 模块线程硬约束

- 每个长连接必须声明 DataConnectionOwner。
- 请求取消后不得写入 State、缓存或 UI。

## 并发冲突策略

- connection owner stop：关闭该 owner 的 connection；普通请求取消由 ParentScope、plugin contribution 或 Host runtime gate 控制。
- 请求取消：返回 Cancelled，不写缓存和状态。

## UI 线程规则

- 非 Presentation 模块不得直接操作 Avalonia visual tree。
- 需要 UI 更新的结果必须通过 Presentation dispatcher、State 或 EventBus contract 间接到达 UI。
- Presentation 的 VisualTree 修改必须在 UI dispatcher 上执行。

## 后台任务和取消

- IO、网络、缓存清理、streaming 和 handler 调用必须观察 cancellation。
- 取消后 pipeline 不得写缓存或确认 optimistic update；应用收到 Cancelled/StaleSuppressed 后不得提交 State、EventBus 或 UI。
- 长连接必须绑定 `DataConnectionOwner`；standalone stream 由可选 ParentScope 或显式 Dispose 结束。

## 死锁规避

- 不在 UI 线程同步等待异步操作。
- 不在 lock 内调用用户 handler、插件代码、dispatcher、transport 或外部 process。
- connection manager 在锁内只发布 lifecycle transaction Task；`IDataConnection.StartAsync/StopAsync` 在锁外执行。外部并发调用共享事务，同一异步调用链重入快速失败。
- manager 按注册逆序停止连接；插件 lease 在停连接后等待在途请求退出，再撤销其余资源。
