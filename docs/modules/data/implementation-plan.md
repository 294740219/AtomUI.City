# AtomUI.City.Data Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-DATA-001 | Yes | Yes | DataPipelineTests | Baseline Exists | Required | 断言执行顺序、取消不写缓存、retry diagnostics。 | Required | Ready to Start Product Implementation |
| AUC-DATA-002 | Yes | Yes | HttpDataTransportTests | Baseline Exists | Required | 断言 status -> DataErrorKind 映射。 | Required | Ready to Start Product Implementation |
| AUC-DATA-003 | Yes | Yes | GrpcDataTransportTests | Baseline Exists | Required | 断言 GrpcStatusCode 映射。 | Required | Ready to Start Product Implementation |
| AUC-DATA-004 | Yes | Yes | SignalRDataTransportTests | Baseline Exists | Required | 断言 invocation context。 | Required | Ready to Start Product Implementation |
| AUC-DATA-005 | Yes | Yes | DataConnectionLifecycleTests | Baseline Exists | Required | 断言状态转换、owner 释放。 | Required | Ready to Start Product Implementation |
| AUC-DATA-006 | Yes | Yes | AccessTokenCredentialProviderTests | Baseline Exists | Required | 断言 credential before transport。 | Required | Ready to Start Product Implementation |
| AUC-DATA-007 | Yes | Yes | DataRequestCacheTests | Baseline Exists | Required | 断言 key 组成和 hit/miss。 | Required | Ready to Start Product Implementation |
| AUC-DATA-008 | Yes | Yes | DataResultTests; DataDiagnosticsTests | Baseline Exists | Required | 断言 result 不混用 success/error。 | Required | Ready to Start Product Implementation |
| AUC-DATA-009 | Yes | Yes | DataRegistrationTests | Baseline Exists | Required | 断言默认服务。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
