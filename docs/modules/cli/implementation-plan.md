# AtomUI.City.Cli Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-CLI-001 | Yes | Yes | CliCommandArchitectureTests | Baseline Exists | Required | 断言入口名、未知命令、缺参、exit code、usage 输出和 JSON 模式隔离。 | Required | Ready to Start Product Implementation |
| AUC-CLI-002 | Yes | Yes | CliNewAppTests | Baseline Exists | Required | 断言生成项目、冲突、非法名称、dry-run、JSON artifacts 和取消。 | Required | Ready to Start Product Implementation |
| AUC-CLI-003 | Yes | Yes | CliBuildAndTestCommandTests | Baseline Exists | Required | 断言成功、失败、非零 exit code、取消、CI 模式和输出截断。 | Required | Ready to Start Product Implementation |
| AUC-CLI-004 | Yes | Yes | CliInspectDoctorPluginTests | Baseline Exists | Required | 断言合法插件、manifest 缺失、版本非法、layout 错误和 JSON diagnostics。 | Required | Ready to Start Product Implementation |
| AUC-CLI-005 | Yes | Yes | CliCommandArchitectureTests | Baseline Exists | Required | 断言 schema、纯 JSON、artifact 列表、suggested commands、retryable 语义。 | Required | Ready to Start Product Implementation |
| AUC-CLI-006 | Yes | Yes | CliCommandArchitectureTests | Baseline Exists | Required | 断言 CI、non-interactive、stdin unavailable、需要确认时失败。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
