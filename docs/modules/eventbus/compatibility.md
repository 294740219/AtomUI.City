# AtomUI.City.EventBus Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- Publish 不隐式切 UI 线程。
- handler 外部代码不能在总线内部锁内执行。
- 订阅必须返回可释放句柄并绑定 owner。
- 跨插件事件类型必须来自 Host 共享 contract 程序集。
- 默认派发顺序稳定。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `IEventBus.PublishAsync` / `PostAsync` 的 null event、预取消 token、disposed bus、publish options、correlation/causation 传播和 delivery/post result 边界进入 1.0 兼容承诺。
- `EventDeliveryResult` 成功结果的 error message 必须为 null；`EventPostResult` accepted 结果的 rejection reason 必须为 null，rejected 结果必须携带非空白 rejection reason。
- `IEventSubscriber.Subscribe` 只保留显式 `LifecycleScope` owner 的异步 delegate 和 `IEventHandler<TEvent>` 两个核心 overload；同步 `Action<EventContext<TEvent>>` 由有 owner 的扩展方法提供。
- 一条 subscription 只有一个 owner；owner running 原子校验、Quiescing barrier、唯一终止事务、StopAsync drain、owner cancellation 和 EventBus dispose 清理进入 1.0 兼容承诺。
- `EventSubscriptionState` 的稳定值为 `Created=0`、`Active=1`、`Quiescing=2`、`Draining=3`、`Disposed=4`、`Faulted=5`；timeout 不形成独立永久状态。
- `IEventContractRegistry` 的 shared-only 注册、默认 descriptor 映射、重复 contract id/type 拒绝和 plugin-private descriptor 拒绝进入 1.0 兼容承诺。
- `EventDispatchPolicy` 的 `Current=0`、`UiThread=1`、`Background=2`、`Serialized=3` 和 `EventErrorPolicy` 的 `ContinueAndReport=0`、`StopPublication=1`、`FailPublisher=2` 进入 1.0 兼容承诺。
- `EventDiagnosticIds` 已登记的 `EventBus.Event*` 字符串、delivery failure/cancellation 的 `contractId`、`eventId`、`subscriptionId` context，以及 subscription quiescing/disposed/termination-failed 的 `subscriptionId` context 进入 1.0 兼容承诺。
- `AddEventBus` 默认注册 `IHostDiagnostics`、`IEventContractRegistry`、`IEventBus`、`IEventPublisher` 和 `IEventSubscriber`，并保留调用方预注册 diagnostics 的 override 行为，进入 1.0 兼容承诺。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。

## 1.0 前 Subscription API 收口

EventBus 尚未正式发布，下列第一版源码入口不进入 1.0 合同，施工时直接移除而不建立长期 obsolete 周期：

- 所有 ownerless `Subscribe<TEvent>` overload。
- `Subscribe<TEvent>(Func<TEvent, CancellationToken, ValueTask>)` 简化入口。
- 核心接口中的同步 `Action<EventContext<TEvent>>` overload；迁移到 `EventSubscriberExtensions.Subscribe` 并显式传入 owner。

迁移规则：应用级静态/DI handler 由 generated registration 自动绑定 `ApplicationScope`；Window、Route、Activation 等动态订阅传入对应 `LifecycleScope`；插件 handler 通过 EventBus contribution contract 注册并由领域 Lease 持有。
