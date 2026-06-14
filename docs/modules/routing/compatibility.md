# AtomUI.City.Routing Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- Routing 只负责 Route -> ViewModel Target。
- RouteGraphSnapshot 发布后不可变。
- 导航是事务，失败不提交半导航。
- 插件路由撤销必须发布新 graph。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。
- 1.0 起 `RouteTemplate.Parse` 必须拒绝 malformed brace、重复参数、非末尾 catch-all 和未知 constraint；放宽这些失败行为属于兼容性风险。
- 1.0 起 `RouteDescriptor.ContributionId`、`RouteGraphSnapshot.GetContributionRoutes` 和 `RouteGraphSnapshot.WithoutContribution` 属于 graph contribution 兼容 contract。
- 同级同 template 路由只有在至少一个候选声明 match policy 时才可共存；改变该冲突规则属于兼容性风险。
- `RouteTemplate.TryMatch` 和 `RouteMatcher.Match/MatchAll` 的 null path 边界、constraint 拒绝语义和并发读能力属于 1.0 兼容 contract。
- `NavigationConcurrencyPolicy` 的 `CancelPrevious`、`Queue`、`RejectIfBusy` 语义和 `NavigationScope` Dispose 后拒绝新导航属于 1.0 兼容 contract。
- Guard hierarchy 顺序、redirect loop 诊断码、`NavigationResult.RedirectTarget` 和 Redirected 结果携带最终 route 的行为属于 1.0 兼容 contract。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
