# AtomUI.City.PluginSystem Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-PLUGIN-001 | Yes | Yes | PluginDeclarationAttributeTests; PluginManifestTests | Baseline Exists | Required | 断言 id、version、mainAssembly、schema 和 required fields。 | Required | Ready to Start Product Implementation |
| AUC-PLUGIN-002 | Yes | Yes | PluginDependencyTests | Baseline Exists | Required | 断言 missing、cycle、version mismatch diagnostics。 | Required | Ready to Start Product Implementation |
| AUC-PLUGIN-003 | Yes | Yes | PluginPackageTests | Baseline Exists | Required | 断言 staging cleanup、installed record、path normalization。 | Required | Ready to Start Product Implementation |
| AUC-PLUGIN-004 | Yes | Yes | PluginLoadingTests | Baseline Exists | Required | 断言 invalid install record diagnostics 且继续扫描其他插件。 | Required | Ready to Start Product Implementation |
| AUC-PLUGIN-005 | Yes | Yes | PluginLoadingTests | Baseline Exists | Required | 断言 Loaded/Failed 状态和 diagnostics。 | Required | Ready to Start Product Implementation |
| AUC-PLUGIN-006 | Yes | Yes | PluginMsBuildContractTests | Baseline Exists | Required | 断言 MSBuild property、output path、package content。 | Required | Ready to Start Product Implementation |
| AUC-PLUGIN-007 | Yes | Yes | PluginResultTests | Baseline Exists | Required | 断言 AUCPLG0000-0021 关键路径。 | Required | Ready to Start Product Implementation |
| AUC-PLUGIN-008 | Yes | Yes | PluginLoadingTests | Baseline Exists | Required | 断言 Disable -> Unloading -> Unloaded/UnloadPending。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
