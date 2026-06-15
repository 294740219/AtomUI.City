# AtomUI.City.Templates Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 生成项目必须 restore、build 和 test。
- 模板变量必须校验。
- 输出不得包含机器绝对路径。
- dry-run 不写文件。
- 应用模板生成的 `<AppName>.slnx`、`Directory.Build.props`、`docs/<AppName>.md`、app project 和 test project 属于 1.0 generated output 兼容面；不得写入机器绝对路径。
- 应用模板生成结果必须能通过 AtomUI.City 本地包源或发布包源执行 restore、build 和 test。
- `TemplateChange.Create` 的路径规范化语义、`TemplatePlan.Validate` 返回的 `AUCTPL1001`、`AUCTPL1002`、`AUCTPL1003` 诊断码和 context 字段属于 1.0 兼容承诺。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `ApplicationTemplateRenderer.Render(ApplicationTemplateOptions, CancellationToken)` 是 1.0 兼容承诺；预取消 token 必须在写入任何文件前抛 `OperationCanceledException`。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
