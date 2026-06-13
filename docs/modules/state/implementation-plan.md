# AtomUI.City.State Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-STATE-001 | Yes | Yes | WritableStateTests | Baseline Exists | Required | 断言原子更新、version、通知顺序、写拒绝。 | Required | Ready to Start Product Implementation |
| AUC-STATE-002 | Yes | Yes | ApplicationStateTests | Baseline Exists | Required | 断言注册、读取、writer、not registered。 | Required | Ready to Start Product Implementation |
| AUC-STATE-003 | Yes | Yes | ComputedStateTests | Baseline Exists | Required | 断言依赖失效、缓存、异常诊断。 | Required | Ready to Start Product Implementation |
| AUC-STATE-004 | Yes | Yes | StateScopeTests; StateThreadingTests | Baseline Exists | Required | 断言 dispose 后不通知。 | Required | Ready to Start Product Implementation |
| AUC-STATE-005 | Yes | Yes | StateSnapshotTests | Baseline Exists | Required | 断言不可变、过滤、restore diagnostics。 | Required | Ready to Start Product Implementation |
| AUC-STATE-006 | Yes | Yes | StateCollectionTests | Baseline Exists | Required | 断言 change kind。 | Required | Ready to Start Product Implementation |
| AUC-STATE-007 | Yes | Yes | StateDiagnosticsTests | Baseline Exists | Required | 断言 AUCSTA001-010。 | Required | Ready to Start Product Implementation |
| AUC-STATE-008 | Yes | Yes | StateThreadingTests | Baseline Exists | Required | 断言不隐式 UI。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
