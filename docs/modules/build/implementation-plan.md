# AtomUI.City.Build Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-BUILD-001 | Yes | Yes | OutputLayoutTests | Baseline Exists | Required | 断言 artifacts、packages、logs、test-results 都在 output 下。 | Required | Ready to Start Product Implementation |
| AUC-BUILD-002 | Yes | Yes | PackageMetadataTests | Baseline Exists | Required | 断言 LGPL v3、repository、symbol、package id 和 dependency group。 | Required | Ready to Start Product Implementation |
| AUC-BUILD-003 | Yes | Yes | ProjectInventoryTests | Baseline Exists | Required | 断言 src/tests 项目被 inventory 覆盖。 | Required | Ready to Start Product Implementation |
| AUC-BUILD-004 | Yes | Yes | ProjectDependencyBoundaryTests | Baseline Exists | Required | 断言 runtime 不依赖 Testing/Roslyn/test packages。 | Required | Ready to Start Product Implementation |
| AUC-BUILD-005 | Yes | Yes | SourceGeneratorProjectStructureTests | Baseline Exists | Required | 断言 generator target、analyzer layout、runtime 不引用 generator。 | Required | Ready to Start Product Implementation |
| AUC-BUILD-006 | Yes | Yes | EngineeringGateTests; PackagingReleaseGateTests | Baseline Exists | Required | 断言 docs、format、pack、test gate 可本地执行。 | Required | Ready to Start Product Implementation |
| AUC-BUILD-007 | Yes | Yes | TestNamingConventionTests | Baseline Exists | Required | 断言测试命名和模块对应关系。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
