# AtomUI.City.Security Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- Security 不实现登录 UI。
- 认证状态以 immutable snapshot 发布。
- 授权评估不操作 UI 或导航。
- access token 失败返回 AccessTokenResult。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `AuthenticationStateStore` 发布 cloned immutable snapshot、等价重复设置幂等、Failed/SignedOut 清除 principal 与 token hint、StateChanged 携带 previous/current snapshot 的行为进入 1.0 兼容承诺。
- `ICurrentPrincipalAccessor.Principal` 返回当前 snapshot principal、已读取 principal 保持稳定、`SecurityPrincipals.Anonymous` 每次返回独立 unauthenticated principal 的行为进入 1.0 兼容承诺。
- `PermissionRegistry` 使用大小写敏感权限名、成功变更递增 revision、重复注册返回 false、`RemoveByContribution` 撤销已有权限并阻止同一 contribution id 后续注册、重复撤销返回 0 的行为进入 1.0 兼容承诺。
- `PermissionChecker` 对未注册或已撤销权限返回 `AuthorizationResultStatus.Failed` 和 `SecurityFailureKind.PermissionNotFound`，不执行 UI、导航或权限外副作用。
- `IAuthorizationEvaluator.EvaluateAsync` 顺序评估 requirement；`EvaluatePolicyAsync` 通过 `IAuthorizationPolicyProvider` 读取 named policy，policy 缺失返回 `PolicyNotFound`，provider 异常返回 `EvaluatorFailed`，预取消不调用 provider。
- `SecurityRouteGuard` 无 policy 时允许导航，Challenge 默认返回 `authentication-required` reject，配置 `SecurityRouteGuardOptions.LoginRouteId` 后返回 redirect；Forbidden 返回 `authorization-forbidden` reject，provider/evaluator 异常返回 `authorization-failed` failed result。
- `CommandAuthorizationSource` 无 descriptor 时允许命令；未授权时按 descriptor 返回 disable/hide 状态；权限 registry、descriptor 和 authentication state 变化发布 `AuthorizationChanged`；provider/evaluator 异常返回 `Failed/EvaluatorFailed`；Dispose 后释放订阅。
- `AccessTokenResultStatus` 区分 `Failed` 与 `Unavailable`；`DelegateAccessTokenProvider` 对未声明异常返回 `Failed` 并保留 exception，预取消返回 `Cancelled` 且不调用 delegate；Data 凭据桥把 Failed token 映射为 credential unavailable。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
