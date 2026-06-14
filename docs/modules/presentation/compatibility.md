# AtomUI.City.Presentation Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 所有 VisualTree 修改必须在 UI dispatcher 上执行。
- `AvaloniaUiDispatcher` 的后台 marshal、取消、`PresentationError.DispatcherUnavailable` 和 dispatcher 诊断上下文字段属于 1.0 兼容 contract。
- ViewLocator 默认使用 generated manifest 或显式注册；`ViewLookupRequest`、`ViewRegistrationOptions.ReplaceExisting`、manifest 原子注册、精确 key lookup 和插件撤销属于 1.0 兼容 contract。
- 插件 View、resource dictionary、localized binding 必须绑定 plugin lease。
- VisualTree 变化必须反馈到 ViewModel/State。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
