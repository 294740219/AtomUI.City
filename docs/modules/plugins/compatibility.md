# AtomUI.City.PluginSystem Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 插件包安装必须先进入 staging，校验成功后原子切换到 installed。
- 插件运行时入口默认一个主 assembly。
- 插件贡献必须有 lease，卸载先 revoke contribution 再释放插件对象。
- 跨插件边界类型必须位于 Host 共享 contract 程序集。
- 插件卸载失败不能破坏 Host，必须进入 UnloadPending 或失败结果。
- 安装路径必须防止路径穿越。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `PluginManifest` 的 required fields、schema `1.x` 支持边界、semantic version、mainAssembly 文件名和 targetFramework 非路径校验进入 1.0 兼容承诺。
- `PluginDependencyValidator` 的 missing dependency、version mismatch、duplicate plugin id 和 cycle diagnostics 进入 1.0 兼容承诺；cycle 中每个 plugin id 必须可定位。
- `PluginPackageInstaller` 的 staging cleanup、installed version root、install record 和规范化 RootPath/ManifestPath 进入 1.0 兼容承诺。
- `PluginDiscoveryScanner` 对 invalid install record、invalid manifest 和 invalid installed directory 的诊断与继续扫描行为进入 1.0 兼容承诺。
- `PluginLoadResult.State`、成功时 `Runtime` 非空、失败时 `Runtime` 为空和 `Faulted` state 进入 1.0 兼容承诺。
- `PluginMsBuildContract` 的 documented properties、manifest output path 和 package content roots 进入 1.0 兼容承诺。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
