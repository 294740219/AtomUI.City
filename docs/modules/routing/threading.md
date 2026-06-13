# AtomUI.City.Routing Threading

## 线程模型范围

执行边界：Host runtime navigation graph。

## 模块线程硬约束

- Routing 只负责 Route -> ViewModel Target。
- RouteGraphSnapshot 发布后不可变。
- 导航是事务，失败不提交半导航。
- 插件路由撤销必须发布新 graph。

## 并发冲突策略

- 模板语法错误：graph build 失败。
- 路由冲突：拒绝发布 graph。
- 参数绑定失败：NavigationResult Failed。

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
