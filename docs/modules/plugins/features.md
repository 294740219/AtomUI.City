# AtomUI.City.PluginSystem Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-PLUGIN-001 | Plugin Metadata | Completed | PluginAttribute, PluginManifest, PluginDescriptor | PluginDeclarationAttributeTests; PluginManifestTests |
| AUC-PLUGIN-002 | Dependency Validation | Completed | PluginDependencyValidator, PluginSemanticVersion | PluginDependencyTests |
| AUC-PLUGIN-003 | Package Installation | Completed | PluginPackageInstaller, PluginInstallationReader, PluginPackagePaths | PluginPackageTests |
| AUC-PLUGIN-004 | Discovery | Ready to Start Product Implementation | PluginDiscoveryScanner | PluginLoadingTests |
| AUC-PLUGIN-005 | Loading | Ready to Start Product Implementation | PluginLoader, PluginLoadResult | PluginLoadingTests |
| AUC-PLUGIN-006 | MSBuild Contract | Ready to Start Product Implementation | PluginMsBuildContract | PluginMsBuildContractTests |
| AUC-PLUGIN-007 | Diagnostics | Ready to Start Product Implementation | PluginDiagnosticIds, PluginDiagnostic | PluginResultTests |
| AUC-PLUGIN-008 | Unload Contract | Ready to Start Product Implementation | PluginRuntime, Contribution lease | PluginLoadingTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| 插件包安装必须先进入 staging，校验成功后原子切换到 installed。 | 必须有实现、测试或工程门禁证据。 |
| 插件运行时入口默认一个主 assembly。 | 必须有实现、测试或工程门禁证据。 |
| 插件贡献必须有 lease，卸载先 revoke contribution 再释放插件对象。 | 必须有实现、测试或工程门禁证据。 |
| 跨插件边界类型必须位于 Host 共享 contract 程序集。 | 必须有实现、测试或工程门禁证据。 |
| 插件卸载失败不能破坏 Host，必须进入 UnloadPending 或失败结果。 | 必须有实现、测试或工程门禁证据。 |
| 安装路径必须防止路径穿越。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-PLUGIN-001 Plugin Metadata

Feature ID: `AUC-PLUGIN-001`
Status: Completed
Goal: 从 attribute 和 manifest 建立插件身份。
Public Contract: PluginAttribute, PluginManifest, PluginDescriptor
Runtime / Build Behavior: 从 attribute 和 manifest 建立插件身份；manifest required fields、schema、version、mainAssembly 和 targetFramework 在加载前校验。
Failure Behavior: id 缺失、required field 缺失、id mismatch、schema 不支持、version/mainAssembly/targetFramework 非法。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `PluginDeclarationAttributeTests; PluginManifestTests`。
Required Assertions: 断言 id、version、mainAssembly、schema、targetFramework 和 required fields。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-PLUGIN-002 Dependency Validation

Feature ID: `AUC-PLUGIN-002`
Status: Completed
Goal: 校验插件依赖、版本范围和循环。
Public Contract: PluginDependencyValidator, PluginSemanticVersion
Runtime / Build Behavior: 校验插件依赖、版本范围和循环；循环中的每个插件都产生诊断。
Failure Behavior: 缺失、循环、版本不满足、重复 plugin id。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `PluginDependencyTests`。
Required Assertions: 断言 missing、cycle、version mismatch、duplicate id diagnostics，并断言 cycle 内每个 plugin id 都可定位。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-PLUGIN-003 Package Installation

Feature ID: `AUC-PLUGIN-003`
Status: Completed
Goal: 安装包布局、staging、installed、rollback。
Public Contract: PluginPackageInstaller, PluginInstallationReader, PluginPackagePaths
Runtime / Build Behavior: 安装包布局、staging、installed、install record 和规范化安装路径。
Failure Behavior: 路径穿越、包布局缺失、重复安装、取消、解压失败。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `PluginPackageTests`。
Required Assertions: 断言 staging cleanup、installed record、path normalization、取消和解压失败。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-PLUGIN-004 Discovery

Feature ID: `AUC-PLUGIN-004`
Status: Ready to Start Product Implementation
Goal: 扫描安装目录并产生 PluginInstallation。
Public Contract: PluginDiscoveryScanner
Runtime / Build Behavior: 扫描安装目录并产生 PluginInstallation。
Failure Behavior: 缺少 install record、非法 record、目录无效。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `PluginLoadingTests`。
Required Assertions: 断言 invalid install record diagnostics 且继续扫描其他插件。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-PLUGIN-005 Loading

Feature ID: `AUC-PLUGIN-005`
Status: Ready to Start Product Implementation
Goal: 加载插件 manifest 和主 assembly。
Public Contract: PluginLoader, PluginLoadResult
Runtime / Build Behavior: 加载插件 manifest 和主 assembly。
Failure Behavior: 主程序集缺失、manifest invalid、id mismatch。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `PluginLoadingTests`。
Required Assertions: 断言 Loaded/Failed 状态和 diagnostics。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-PLUGIN-006 MSBuild Contract

Feature ID: `AUC-PLUGIN-006`
Status: Ready to Start Product Implementation
Goal: 插件包属性、manifest 输出和 package layout。
Public Contract: PluginMsBuildContract
Runtime / Build Behavior: 插件包属性、manifest 输出和 package layout。
Failure Behavior: 属性缺失、layout 不合法、manifest 未生成。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `PluginMsBuildContractTests`。
Required Assertions: 断言 MSBuild property、output path、package content。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-PLUGIN-007 Diagnostics

Feature ID: `AUC-PLUGIN-007`
Status: Ready to Start Product Implementation
Goal: 插件安装、发现、加载和依赖诊断。
Public Contract: PluginDiagnosticIds, PluginDiagnostic
Runtime / Build Behavior: 插件安装、发现、加载和依赖诊断。
Failure Behavior: 诊断码不能复用，context 必须有 pluginId/path。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `PluginResultTests`。
Required Assertions: 断言 AUCPLG0000-0021 关键路径。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-PLUGIN-008 Unload Contract

Feature ID: `AUC-PLUGIN-008`
Status: Ready to Start Product Implementation
Goal: 撤销贡献并释放插件运行时。
Public Contract: PluginRuntime, Contribution lease
Runtime / Build Behavior: 撤销贡献并释放插件运行时。
Failure Behavior: active contribution、未释放 view/subscription/connection。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `PluginLoadingTests`。
Required Assertions: 断言 Disable -> Unloading -> Unloaded/UnloadPending。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
