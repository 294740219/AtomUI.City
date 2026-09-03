# AtomUI.City.EventBus Features

本文件是 EventBus 唯一 Feature 来源。没有 Feature ID 的能力不能进入实现；专题设计、API contract、诊断和测试矩阵必须引用本文件中的 Feature ID。

EventBus 当前处于设计收口阶段。下列 Feature 均为 `In Design`，不能由既有源码或测试文件的存在推导为产品级完成。

## Feature 索引

| Feature ID | 名称 | 阶段 | 状态 | Contract Family | 主测试族 |
| --- | --- | --- | --- | --- | --- |
| AUC-EVENTBUS-001 | Typed Publication | Application Plane MVP | In Design | IEventPublisher, EventPublishOptions, EventContext, EventPublishResult, EventPostResult | EventPublicationTests |
| AUC-EVENTBUS-002 | Subscription Ownership & Lifecycle | Application Plane MVP | In Design | IEventSubscriber, IEventHandler, IEventSubscription, subscription state/options/descriptor | EventSubscriptionLifecycleTests |
| AUC-EVENTBUS-003 | Contract Identity & Registry | Application Plane MVP | In Design | EventContractId, EventContractDescriptor, EventContractPlane, IEventContractRegistry | EventContractRegistryTests |
| AUC-EVENTBUS-004 | Dispatch & Failure Policy | Application Plane MVP | In Design | dispatch target/mode, delivery result, error and timeout policy | EventDispatchAndFailurePolicyTests |
| AUC-EVENTBUS-005 | Diagnostics & Observability | Application Plane MVP | In Design | EventDiagnosticIds, diagnostic context, metrics and safe payload projection | EventDiagnosticsTests |
| AUC-EVENTBUS-006 | DI & Host Lifecycle | Application Plane MVP | In Design | AddEventBus, EventBus module/options, internal lifecycle control plane | EventBusHostIntegrationTests |
| AUC-EVENTBUS-007 | Bounded Channel Runtime | Application Plane MVP | In Design | EventChannel, channel options, execution/concurrency/backpressure policy | EventChannelRuntimeTests |
| AUC-EVENTBUS-008 | Generated Event Catalog & NativeAOT | Application Plane MVP | In Design | event attributes, generated registrar/manifest/descriptors/invokers | EventBusGeneratorTests; EventBusNativeAotProcessTests |
| AUC-EVENTBUS-009 | Plugin Event Planes | Plugin Integration Phase | In Design | shared/private plane, capability, EventBus contribution controller and domain lease | EventBusPluginContractTests; EventBusPluginLifecycleTests |

## 完成口径

- `AUC-EVENTBUS-001` 至 `AUC-EVENTBUS-008` 全部达到 `Verified`，表示 EventBus Application Plane MVP 完成。
- `AUC-EVENTBUS-009` 达到 `Verified`，表示 EventBus Plugin Plane 完成。
- Plugin Plane 的 EventBus 侧接口和测试替身在当前设计阶段冻结；真实动态插件、AssemblyLoadContext drain/unload 和 capability 集成等待 PluginSystem 施工，不阻断 Application Plane MVP。
- 性能 Benchmark、确定性调度器和 CLI dogfood 是横向验收门禁，不单独建立运行时 Feature。

## Feature 硬门禁

| 约束 | 归属 | 验收要求 |
| --- | --- | --- |
| Publish 不提供阻塞式同步入口，也不隐式切换 UI 线程。 | 001, 004 | 必须有 API contract、线程测试和真实 Host 验证。 |
| handler 外部代码不能在 EventBus 内部锁内执行。 | 002, 004, 007 | 必须覆盖 publish/subscribe/stop 并发和重入。 |
| 所有订阅都有明确 owner，并返回可停止、可释放句柄。 | 002 | 必须覆盖 owner stop、drain、timeout、重复释放和 Dispose 后行为。 |
| 所有异步 channel 和订阅队列有界。 | 007 | 必须覆盖容量、背压、取消、关闭及资源上限。 |
| Handler 错误、取消、丢弃和超时不能静默丢失。 | 004, 005, 007 | 必须产生声明的 Result、异常或稳定诊断。 |
| 跨插件事件对象图只使用 Host 共享 contract。 | 003, 009 | 必须在插件代码执行前校验，并覆盖卸载残留。 |
| 默认生产路径不扫描程序集或反射调用 handler。 | 008 | 必须覆盖 generated catalog、静态 invoker、trimming 和 NativeAOT。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract，不能把核心 API 留给施工阶段决定。
- Feature 必须定义非法输入、状态非法、取消、重复调用、并发冲突和释放后行为。
- Feature 必须定义诊断码或明确说明由哪个上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、build 或 generator 的功能必须增加专项测试。
- Feature 只有在文档、实现、测试、诊断、兼容性和发布门禁全部提供证据后才能标记为 `Verified`。

## AUC-EVENTBUS-001 Typed Publication

Feature ID: `AUC-EVENTBUS-001`
Status: In Design
Goal: 提供强类型、可取消、可观察完成状态的进程内事件发布。
Contract Family: `IEventPublisher`、`EventPublishOptions`、`EventContext<TEvent>`、`EventPublishResult`、`EventPostResult`。
Design Coverage: `PublishAsync` 等待当前 delivery plan 完成；`PostAsync` 只等待受管有界 channel 接受；定义 EventId、correlation/causation、publish depth、订阅快照时点及无订阅者语义。
Failure Coverage: null/default 输入、非法 options、预取消、总线停止、channel 拒绝、结果模型非法状态。
Primary Tests: `EventPublicationTests`。

## AUC-EVENTBUS-002 Subscription Ownership & Lifecycle

Feature ID: `AUC-EVENTBUS-002`
Status: In Design
Goal: 把每个 handler 的所有权、创建上下文、停止、drain 和释放纳入唯一生命周期事务。
Contract Family: `IEventSubscriber`、`IEventHandler<TEvent>`、`IEventSubscription`、subscription state/options/descriptor。
Design Coverage: 每条订阅只有一个 owner；静态/DI 订阅由框架绑定 ApplicationScope，Application Plane 动态订阅显式绑定 LifecycleScope，插件订阅绑定 EventBus 领域 ContributionLease；第一版强引用；定义 Created、Active、Quiescing、Draining、Disposed、Faulted 和唯一终止事务。
Failure Coverage: null/stopped owner、owner/EventBus stop 与注册提交竞争、in-flight handler、等待取消或 drain timeout、handler 创建/释放失败、重复及并发 Stop/Dispose/owner cancellation。
Primary Tests: `EventSubscriptionLifecycleTests`。

## AUC-EVENTBUS-003 Contract Identity & Registry

Feature ID: `AUC-EVENTBUS-003`
Status: In Design
Goal: 为事件提供独立于临时 CLR 名称的稳定身份、版本和加载边界。
Contract Family: `EventContractId`、`EventContractDescriptor`、`EventContractPlane`、`IEventContractRegistry`。
Design Coverage: shared/private plane、contract version、type mapping、schema/assembly identity、冻结和动态 snapshot、精确类型匹配。
Failure Coverage: default/重复 id、重复 type、版本不兼容、私有类型进入 Shared Plane、错误 AssemblyLoadContext。
Primary Tests: `EventContractRegistryTests`。

## AUC-EVENTBUS-004 Dispatch & Failure Policy

Feature ID: `AUC-EVENTBUS-004`
Status: In Design
Goal: 明确 handler 的执行目标、完成语义、错误隔离、取消和超时行为。
Contract Family: dispatch target/mode、delivery result、error policy、handler timeout policy。
Design Coverage: Current、UiThread、Background 和受管 Serialized dispatch；ContinueAndReport、StopPublication、FailPublisher、DisableSubscription 的适用边界和配置优先级。
Failure Coverage: dispatcher unavailable、handler failure/cancellation/timeout、PostAsync 后台失败、错误策略冲突和 cleanup failure。
Primary Tests: `EventDispatchAndFailurePolicyTests`。

## AUC-EVENTBUS-005 Diagnostics & Observability

Feature ID: `AUC-EVENTBUS-005`
Status: In Design
Goal: 用稳定诊断和指标重建 publication、delivery、subscription、channel 与插件因果链。
Contract Family: `EventDiagnosticIds`、EventBus diagnostic context、metrics snapshot、safe payload projector。
Design Coverage: EventId、CorrelationId、CausationId、ContractId、SubscriptionId、owner、channel、partition、policy 和 duration。
Failure Coverage: 诊断 sink 不可用、payload/exception 持有插件对象、限流、丢弃、超时和卸载残留。
Primary Tests: `EventDiagnosticsTests`。

## AUC-EVENTBUS-006 DI & Host Lifecycle

Feature ID: `AUC-EVENTBUS-006`
Status: In Design
Goal: 将 EventBus 作为 ApplicationScope 运行时模块接入 Core Build、Start、Stop 和 Dispose。
Contract Family: `AddEventBus`、EventBus module/options、只读业务接口、internal lifecycle controller。
Design Coverage: descriptor 收集、配置冻结、worker 启停、拒绝新操作、总 deadline 下的 drain、Root DI 能力隔离。
Failure Coverage: 重复注册、配置非法、启动中途失败、Stop-before-Start、并发 Stop/Dispose 和清理聚合失败。
Primary Tests: `EventBusHostIntegrationTests`。

## AUC-EVENTBUS-007 Bounded Channel Runtime

Feature ID: `AUC-EVENTBUS-007`
Status: In Design
Goal: 用有界 channel 提供声明式顺序、并发、partition 和背压控制。
Contract Family: `EventChannel<TEvent>`、channel options、execution/concurrency/backpressure policy。
Design Coverage: Serialized、Partitioned、Concurrent；Wait、Reject、DropOldest、DropNewest、CoalesceLatest；有限 capacity、并发度、partition 数量和回收规则。
Failure Coverage: queue full/closed、等待取消或超时、drop/coalesce 诊断、递归发布、serialized 自等待和 shutdown drain。
Primary Tests: `EventChannelRuntimeTests`。

## AUC-EVENTBUS-008 Generated Event Catalog & NativeAOT

Feature ID: `AUC-EVENTBUS-008`
Status: In Design
Goal: 在编译期生成 contract、handler、channel 和 manifest 元数据，并接入唯一生产注册流程。
Contract Family: event/handler/owner attributes、generated registrar/manifest、descriptor 和 strongly typed invoker。
Design Coverage: 多程序集 catalog 合并、稳定 registrar identity、Module owner、生成代码 Host 接入、manifest version、无运行时扫描或 `MethodInfo.Invoke`。
Failure Coverage: owner 缺失/冒充、重复 ContractId、非法对象图、非法 plane/capability、manifest 版本和 registrar 冲突。
Primary Tests: `EventBusGeneratorTests`、`EventBusNativeAotProcessTests`。

## AUC-EVENTBUS-009 Plugin Event Planes

Feature ID: `AUC-EVENTBUS-009`
Status: In Design
Goal: 允许 PluginSystem 通过受控协议使用 Shared Plane，并为单个插件建立可整体释放的 Private Plane。
Contract Family: plugin event capability、EventBus contribution request/controller/domain lease、private plane runtime contract。
Design Coverage: EventBus 不依赖 PluginSystem 具体实现；EventBus 侧先冻结接口和测试替身；真实动态插件集成在 PluginSystem 阶段完成。
Failure Coverage: capability denied、contract/version 不兼容、半激活回滚、quiescing、drain timeout、私有类型泄漏和 ALC 无法回收。
Primary Tests: `EventBusPluginContractTests`；PluginSystem 可用后增加 `EventBusPluginLifecycleTests`。
