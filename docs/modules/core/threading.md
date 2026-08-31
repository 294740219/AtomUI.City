# AtomUI.City.Core Threading

## 线程模型范围

执行边界：Host runtime kernel。

## 模块线程硬约束

- Core 不允许引用 Avalonia、AtomUI、Roslyn、CLI、Templates 或 Testing 生产程序集。
- ApplicationHostBuilder Build 后必须冻结服务注册入口。
- 模块配置阶段禁止 BuildServiceProvider 和运行期服务解析。
- IUiDispatcher 只定义抽象，Core 不提交真实 UI work。

## 并发冲突策略

- 重复 module id：Build 失败，诊断包含已有 module 和重复 module。

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
- Host、LifecycleScope 和 ModuleRegistry 只在 lock 内检查状态并发布唯一 lifecycle transaction Task；middleware、module hook、cancellation callback 和 dispose 必须在 lock 外执行。
- lifecycle transaction Task 必须先于 Starting、Stopping 或 Disposing 状态对并发调用者可见；外部并发调用共享事务，事务内部对同一 owner 的公共 lifecycle API 重入必须快速失败。
- 异步调用链使用可嵌套 invocation frame 识别递归；同步 cancellation callback 使用同步 frame 补偿其注册时捕获的 ExecutionContext。
- 释放顺序从 leaf owner 到 parent owner。
