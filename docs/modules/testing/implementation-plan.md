# AtomUI.City.Testing Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-TESTING-001 | Yes | Yes | TestHostTests | Product Implementation Exists | Verified | 断言 service、diagnostics、dispose、records。 | None for Phase 2 slice | Implemented |
| AUC-TESTING-002 | Yes | Yes | FakeUiDispatcherTests | Product Implementation Exists | Verified | 断言 queue、UI 线程识别、异常、pending count。 | None for Phase 2 slice | Implemented |
| AUC-TESTING-003 | Yes | Yes | SharedTestUtilitiesTests | Product Implementation Exists | Verified | 断言虚拟时间推进、任务顺序、异常记录。 | None for Phase 2 slice | Implemented |
| AUC-TESTING-004 | Yes | Yes | ModuleTestHostTests | Product Implementation Exists | Verified | 断言 module graph、lifecycle、diagnostics。 | None for Phase 2 slice | Implemented |
| AUC-TESTING-005 | Yes | Yes | PluginTestHostTests | Product Implementation Exists | Verified | 断言 load/unload、contribution、owner revoke。 | None for Phase 2 slice | Implemented |
| AUC-TESTING-006 | Yes | Yes | RoutingTestHostTests | Product Implementation Exists | Verified | 断言 route build、match、navigation helper。 | None for Phase 2 slice | Implemented |
| AUC-TESTING-007 | Yes | Yes | SourceGenerationTestKitTests | Product Implementation Exists | Verified | 断言 generated source snapshot、diagnostics、references。 | None for Phase 2 slice | Implemented |
| AUC-TESTING-008 | Yes | Yes | AotCompatibilityCheckTests | Baseline Exists | Required | 断言反射扫描、dynamic code、trimming 风险诊断。 | Required | Ready to Start Product Implementation |
| AUC-TESTING-009 | Yes | Yes | TestLayerTests | Baseline Exists | Required | 断言 Unit/Contract/Integration/Platform/Dogfood 分层标记。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
