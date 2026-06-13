# AtomUI.City.Generators Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-GENERATORS-001 | Yes | Yes | IncrementalGeneratorInfrastructureTests | Baseline Exists | Required | 断言 incremental 输入隔离、hint name 稳定、无 runtime 依赖。 | Required | Ready to Start Product Implementation |
| AUC-GENERATORS-002 | Yes | Yes | ModuleDependencyGraphBuilderTests; ModuleMetadataReaderTests | Baseline Exists | Required | 断言 DependsOn 图、循环诊断、默认 module id。 | Required | Ready to Start Product Implementation |
| AUC-GENERATORS-003 | Yes | Yes | ServiceRegistrationManifestBuilderTests; ServiceRegistrationMetadataReaderTests | Baseline Exists | Required | 断言 lifetime、ExposeServices、显式注册和冲突诊断。 | Required | Ready to Start Product Implementation |
| AUC-GENERATORS-004 | Yes | Yes | RouteManifestBuilderTests; RouteMetadataReaderTests | Baseline Exists | Required | 断言 route attribute、template、target、排序和诊断。 | Required | Ready to Start Product Implementation |
| AUC-GENERATORS-005 | Yes | Yes | PluginManifestBuilderTests; PluginMetadataReaderTests | Baseline Exists | Required | 断言 plugin metadata、capability、dependency、contribution。 | Required | Ready to Start Product Implementation |
| AUC-GENERATORS-006 | Yes | Yes | LocalizationManifestBuilderTests; LocalizationMetadataReaderTests | Baseline Exists | Required | 断言 culture、resource、fallback、重复 key 诊断。 | Required | Ready to Start Product Implementation |
| AUC-GENERATORS-007 | Yes | Yes | PresentationViewManifestBuilderTests; PresentationViewRegistrarSourceBuilderTests | Baseline Exists | Required | 断言 ViewFor、constructor、registrar source 和诊断。 | Required | Ready to Start Product Implementation |
| AUC-GENERATORS-008 | Yes | Yes | GeneratorDiagnosticTests | Baseline Exists | Required | 断言 diagnostic id、severity、message args 和 source location。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
