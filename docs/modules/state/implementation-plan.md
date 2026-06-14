# AtomUI.City.State Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | 已文档化 | API 合同 | 现有测试文件 | 实现基线 | 产品合同测试 | 必要断言 | 实现缺口 | 状态 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-STATE-001 | 是 | 是 | WritableStateTests | 已有基线 | 部分通过 | 断言原子更新、version、提交后通知、相等值不通知、订阅 dispose、disposed mutation rejection、updater 异常诊断和写拒绝/access policy。 | 仍需完整产品合同测试 | 产品化进行中 |
| AUC-STATE-002 | 是 | 是 | ApplicationStateTests; StateDefinitionTests | 已有基线 | 部分通过 | 断言注册、读取、writer、not registered、StateDefinition enum 和 schema version 边界。 | 仍需完整产品合同测试 | 产品化进行中 |
| AUC-STATE-003 | 是 | 是 | ComputedStateTests | 已有基线 | 部分通过 | 断言 lazy invalidation、依赖失效、缓存、异常诊断、null dependency 拒绝。 | 仍需完整产品合同测试 | 产品化进行中 |
| AUC-STATE-004 | 是 | 是 | StateScopeTests; StateThreadingTests | 已有基线 | 部分通过 | 断言 dispose 后不通知、Background 不阻塞状态提交、Background handler 失败诊断。 | 仍需完整产品合同测试 | 产品化进行中 |
| AUC-STATE-005 | 是 | 是 | StateSnapshotTests | 已有基线 | 部分通过 | 断言不可变、过滤、restore diagnostics、entry version/schema 边界、entries 不含 null。 | 仍需完整产品合同测试 | 产品化进行中 |
| AUC-STATE-006 | 是 | 是 | StateCollectionTests | 已有基线 | 部分通过 | 断言 change kind、item version、collection version、快照不可变、非法构造参数。 | 仍需完整产品合同测试 | 产品化进行中 |
| AUC-STATE-007 | 是 | 是 | StateDiagnosticsTests | 已有基线 | 必需 | 断言 AUCSTA001-010。 | 必需 | 准备开始产品实现 |
| AUC-STATE-008 | 是 | 是 | StateThreadingTests | 已有基线 | 必需 | 断言不隐式 UI。 | 必需 | 准备开始产品实现 |

## 更新规则

- `已有基线` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `产品合同测试` 为 `必需` 时，不能把 Feature 标记为已实现。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
