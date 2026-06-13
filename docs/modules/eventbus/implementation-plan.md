# AtomUI.City.EventBus Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-EVENTBUS-001 | Yes | Yes | EventPublicationTests | Baseline Exists | Required | 断言 delivery result、error policy、diagnostics。 | Required | Ready to Start Product Implementation |
| AUC-EVENTBUS-002 | Yes | Yes | EventSubscriptionTests | Baseline Exists | Required | 断言 dispose 后不再收到事件。 | Required | Ready to Start Product Implementation |
| AUC-EVENTBUS-003 | Yes | Yes | EventContractRegistryTests | Baseline Exists | Required | 断言 shared contract、private plugin type 拒绝。 | Required | Ready to Start Product Implementation |
| AUC-EVENTBUS-004 | Yes | Yes | EventDispatchingTests | Baseline Exists | Required | 断言顺序、异常聚合、停止策略。 | Required | Ready to Start Product Implementation |
| AUC-EVENTBUS-005 | Yes | Yes | EventDiagnosticsTests | Baseline Exists | Required | 断言 EventBus.Event* 现有代码。 | Required | Ready to Start Product Implementation |
| AUC-EVENTBUS-006 | Yes | Yes | EventBusRegistrationTests | Baseline Exists | Required | 断言默认服务和可替换 diagnostics。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
