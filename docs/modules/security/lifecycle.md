# AtomUI.City.Security Lifecycle

## 生命周期范围

执行边界：Host runtime security service。

AtomUI.City.Security 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- Authentication 使用 Unknown、Anonymous、Authenticating、Authenticated、Refreshing、Expired、SignedOut、Failed 状态词汇；箭头流程由应用认证编排器决定，Store 只校验单个 snapshot 内容，不强制转换图。
- Authorization 是无状态评估，结果为 Allowed / Denied / Forbidden / Challenge / Failed / Cancelled；源码没有 Created/Evaluating 生命周期对象。
- CommandAuthorizationSource: Constructing -> Subscribed -> Disposed；构造期任一订阅失败时先隔离 source，再逆序回滚所有已尝试订阅并聚合/诊断回滚失败；Dispose 幂等，尝试释放全部 Authentication/Permission/Descriptor 订阅、完成通知队列，并聚合退订失败。
- Account session 的 Restoring/Switching/Active 状态属于 Planned `AUC-SECURITY-009`，当前源码不产生。

## 生命周期流程

- AuthenticationStateStore 发布 snapshot。
- PermissionRegistry 注册 permission。
- AuthorizationEvaluator 评估 policy。
- RouteGuard 映射 result。
- CommandAuthorizationSource 订阅上游 revision；Dispose 后停止发布。
- Security diagnostics 写入 Core `IHostDiagnostics`，但不拥有或 Complete 它。

## Host Shutdown / 执行结束行为

- DI 容器释放 `CommandAuthorizationSource`，它先标记 Disposed，再尝试解除全部上游订阅并完成自己的通知队列；退订失败在清理完成后以 `AggregateException` 报告并写诊断。
- AuthenticationStateStore、PermissionRegistry 和内存 provider 不拥有后台任务或外部资源，由 Host singleton 生命周期回收。
- 具体认证/token provider 如创建后台 refresh，必须由其 owner 取消；当前默认 provider 不创建后台任务。
- Planned 文件 store/session manager 必须在 Host 停止时取消 IO 和切换事务。

## 插件动态变更行为

- 当前内存权限、policy、route 和 command provider 通过 contribution id 撤销，并在当前实例内拒绝该 contribution 重新注册。
- 完整 `ContributionLease`/Plugin owner 编排属于未来 PluginSystem 集成，不是 Security 当前 public API。
- 跨插件 contract 类型必须来自 Host 共享程序集。

## 异常中断行为

- 权限未注册：授权失败。
- policy provider 缺失：返回 Failed。
- token provider 不可用：返回 Unavailable。
- 观察者失败：隔离、诊断并继续 drain，不回滚已提交状态。
- 调用方未取消时 provider 抛 OperationCanceledException：按 Failed 处理。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
