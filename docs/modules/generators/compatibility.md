# AtomUI.City.Generators Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- Generator target 为 netstandard2.0 并作为 analyzer 分发。
- Generator 不引用 AtomUI.City 运行时包。
- 输出确定性排序。
- 诊断 id 稳定。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `PresentationViewMetadata.HasAmbiguousConstructors` 是 1.0 前新增 metadata 成员；后续不能删除或改为允许 ambiguous registrar generation。
- generated presentation view registrar 输出 `constructorParameterTypes` 传递属于 1.0 兼容 contract。
- `GeneratorDiagnostics.CreateRoslynDiagnostic` 是 1.0 前新增 diagnostic factory；category、severity、message formatting 和 location fallback 属于兼容行为。
- `AUCANL0001` 默认以 Error 阻止非测试 City 项目调用或引用 `BuildServiceProvider`、主动调用 `IServiceProviderFactory<T>.CreateServiceProvider`，以及调用或引用 Microsoft Generic Host 构建/启动入口；诊断 ID、默认 severity、City ApplicationHost、已有 IHost 启动、测试项目和 generated code 豁免属于兼容行为。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
