# AtomUI.City.PluginSystem Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 插件包安装必须先进入 staging，校验成功后原子切换到 installed。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 插件运行时入口默认一个主 assembly。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 插件贡献必须有 lease，卸载先 revoke contribution 再释放插件对象。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 跨插件边界类型必须位于 Host 共享 contract 程序集。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 插件卸载失败不能破坏 Host，必须进入 UnloadPending 或失败结果。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 安装路径必须防止路径穿越。 | 至少一个明确测试断言，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-PLUGIN-001 | PluginLifecycle | PluginDeclarationAttributeTests; PluginManifestTests | 断言 id、version、mainAssembly、schema、targetFramework 和 required fields。 | id 缺失、required field 缺失、id mismatch、schema 不支持。 | Completed |
| AUC-PLUGIN-002 | PluginLifecycle | PluginDependencyTests | 断言 missing、cycle、version mismatch、duplicate id diagnostics，并断言 cycle 内每个 plugin id 都可定位。 | 缺失、循环、版本不满足、重复 plugin id。 | Completed |
| AUC-PLUGIN-003 | PluginLifecycle | PluginPackageTests | 断言 staging cleanup、installed record、path normalization。 | 路径穿越、包布局缺失、重复安装、取消。 | Required |
| AUC-PLUGIN-004 | PluginLifecycle | PluginLoadingTests | 断言 invalid install record diagnostics 且继续扫描其他插件。 | 缺少 install record、非法 record、目录无效。 | Required |
| AUC-PLUGIN-005 | PluginLifecycle | PluginLoadingTests | 断言 Loaded/Failed 状态和 diagnostics。 | 主程序集缺失、manifest invalid、id mismatch。 | Required |
| AUC-PLUGIN-006 | PluginLifecycle | PluginMsBuildContractTests | 断言 MSBuild property、output path、package content。 | 属性缺失、layout 不合法、manifest 未生成。 | Required |
| AUC-PLUGIN-007 | PluginLifecycle | PluginResultTests | 断言 AUCPLG0000-0021 关键路径。 | 诊断码不能复用，context 必须有 pluginId/path。 | Required |
| AUC-PLUGIN-008 | PluginLifecycle | PluginLoadingTests | 断言 Disable -> Unloading -> Unloaded/UnloadPending。 | active contribution、未释放 view/subscription/connection。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
