# AtomUI.City.Localization Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 语言包按当前 culture 懒加载。
- provider 必须可撤销。
- 缺失 key 必须诊断并走 fallback。
- 插件语言包卸载后不得出现在 lookup。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `LocalizationOptions.DefaultCulture`、`DefaultUICulture` 和 `FallbackCultures` 的默认值与校验行为进入 1.0 兼容承诺。
- `LocalizationService.SetCultureAsync` 的 fallback chain 顺序、非法 culture 失败 result、fallback self-cycle 拒绝和重复 culture 幂等行为进入 1.0 兼容承诺。
- `LanguagePackageRegistry` 的 owner 绑定、重复 package 拒绝和 owner revoke 后拒绝新注册行为进入 1.0 兼容承诺。
- `FileLanguagePackageProvider` 与 `AssemblyLanguagePackageProvider` 的取消结果、格式错误 result 和 culture mismatch result 进入 1.0 兼容承诺。
- `LocalizationService` 的 culture/package cache key、同一 culture/package in-flight load 合并和 lookup load failure fallback 行为进入 1.0 兼容承诺。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
