# AtomUI.City.EventBus Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | 已文档化 | API 合同 | 现有测试文件 | 实现基线 | 产品合同测试 | 必要断言 | 实现缺口 | 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-EVENTBUS-001 | 是 | 是 | EventPublicationTests | 已有基线 | 部分通过 | 断言 delivery/post result 边界、null event、预取消 token、disposed bus、publish options 边界、result immutable/null delivery、error policy、diagnostics。 | 仍需完整产品合同测试 | 产品化进行中 |
| AUC-EVENTBUS-002 | 是 | 是 | EventSubscriptionTests | 已有基线 | 部分通过 | 断言 dispose 后不再收到事件、StopAsync 移除新发布快照、等待 in-flight handler、owner stop/cancellation 释放、bus dispose 清理 active subscriptions、已 Disposed 后 StopAsync 幂等。 | 仍需完整产品合同测试 | 产品化进行中 |
| AUC-EVENTBUS-003 | 是 | 是 | EventContractRegistryTests | 已有基线 | 部分通过 | 断言 shared contract assembly match、重复 contract id、稳定默认映射、plugin-private descriptor default id 拒绝、shared registry 拒绝 plugin-private descriptor。 | 仍需完整产品合同测试 | 产品化进行中 |
| AUC-EVENTBUS-004 | 是 | 是 | EventDispatchingTests | 已有基线 | 部分通过 | 断言顺序、异常聚合、停止策略、未知 error policy 拒绝。 | 仍需完整产品合同测试 | 产品化进行中 |
| AUC-EVENTBUS-005 | 是 | 是 | EventDiagnosticsTests | 已有基线 | 部分通过 | 断言 EventBus.Event* 现有代码、failure/cancellation 诊断包含 contract id、event id 和 subscription id。 | 仍需完整产品合同测试 | 产品化进行中 |
| AUC-EVENTBUS-006 | 是 | 是 | EventBusRegistrationTests | 已有基线 | 必需 | 断言默认服务、可替换 diagnostics 和 provider dispose 释放 EventBus singleton。 | 必需 | 准备开始产品实现 |

## 更新规则

- `已有基线` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `产品合同测试` 为 `必需` 时，不能把 Feature 标记为已实现。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。

## 2026-06-14 进展

- 加固 `EventPublishOptions` 与 `EventContext<TEvent>` 边界：publish depth、correlation id、causation id、event id、contract id、subscription id 与 dispatch policy 的非法输入均由构造或 init 边界拒绝。
- 加固 `EventDeliveryResult` 与 `EventPostResult` record init mutation 边界，确保 `with { ... }` 不能绕过 subscription id、dispatch policy、event id、contract id 或 accepted/rejection 状态一致性校验。
- 加固 `IEventBus` / `InMemoryEventBus` dispose 生命周期：Dispose 幂等，释放 active subscriptions，Dispose 后 publish/post/subscribe 拒绝，DI provider dispose 会释放 singleton bus。
- 加固 `EventContractDescriptor.PluginPrivate<TEvent>` 边界，拒绝 default `EventContractId`。
