# AtomUI.City.PluginSystem Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Current Code | Name | Source |
| --- | --- | --- |
| `AUCPLG0000` | ManifestNotFound | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0001` | MissingPluginId | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0002` | MainAssemblyNotFound | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0003` | PluginIdMismatch | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0004` | UnsupportedManifestSchema | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0005` | RequiredContributionManifestNotFound | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0006` | InvalidMainAssembly | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0007` | PluginAlreadyInstalled | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0008` | PluginDependencyMissing | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0009` | PluginDependencyCycle | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0010` | InvalidPluginId | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0011` | InvalidPluginVersion | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0012` | InvalidContributionPath | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0013` | InvalidTargetFramework | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0014` | PackageExtractionFailed | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0015` | PluginIdConflict | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0016` | PluginDependencyVersionMismatch | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0017` | InvalidInstallRecord | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0018` | PluginVersionMismatch | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0019` | InvalidManifest | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0020` | PluginPackageIdMismatch | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |
| `AUCPLG0021` | MissingInstallRecord | `src/AtomUI.City.PluginSystem/PluginDiagnosticIds.cs` |

## 产品级必须诊断的失败

- manifest 缺失或 schema 不兼容：拒绝安装或加载。
- 依赖缺失或版本不满足：插件保持 Installed，不进入 Enabled。
- 贡献撤销失败：继续撤销其他贡献并报告 UnloadPending。
- 路径穿越或包布局非法：拒绝安装并清理 staging。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`attempt`、`transportKind`。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，implementation plan 必须记录为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.PluginSystem.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
