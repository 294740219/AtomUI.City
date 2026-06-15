# AtomUI.City.Security Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-SECURITY-001 | Authentication State Store | Completed | AuthenticationStateStore, AuthenticationStateSnapshot | AuthenticationStateTests |
| AUC-SECURITY-002 | Current Principal Access | Completed | ICurrentPrincipalAccessor, SecurityPrincipals | AuthenticationStateTests |
| AUC-SECURITY-003 | Permission Registry and Checker | Completed | PermissionRegistry, IPermissionChecker | PermissionRegistryTests; PermissionCheckerTests |
| AUC-SECURITY-004 | Authorization Policy Evaluation | Completed | AuthorizationEvaluator, AuthorizationPolicy | AuthorizationEvaluatorTests; AuthorizationPolicyTests |
| AUC-SECURITY-005 | Route Authorization Guard | Completed | SecurityRouteGuard, IRouteAuthorizationPolicyProvider | RouteAuthorizationGuardTests |
| AUC-SECURITY-006 | Command Authorization | Ready to Start Product Implementation | CommandAuthorizationSource, CommandAuthorizationDescriptor | CommandAuthorizationSourceTests |
| AUC-SECURITY-007 | Access Token Provider | Ready to Start Product Implementation | IAccessTokenProvider, AccessTokenResult | SecurityRegistrationTests |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| Security 不实现登录 UI，只定义状态、权限、授权和 token contract。 | 必须有实现、测试或工程门禁证据。 |
| 认证状态以 immutable snapshot 发布，跨线程读取必须一致。 | 必须有实现、测试或工程门禁证据。 |
| 授权评估不操作 UI、不执行导航、不访问 VisualTree。 | 必须有实现、测试或工程门禁证据。 |
| Route、Command、Data 只通过 Security public contract 集成。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-SECURITY-001 Authentication State Store

Feature ID: `AUC-SECURITY-001`
Status: Completed
Goal: 集中表达当前用户、认证状态和状态变更通知。
Public Contract: AuthenticationStateStore, AuthenticationStateSnapshot
Runtime / Build Behavior: 认证状态以 cloned immutable snapshot 发布；Unknown、Anonymous、Authenticated、Refreshing、SignedOut、Failed 必须清晰区分。
Failure Behavior: provider 失败不能产生半认证状态；Failed 和 SignedOut 必须清除 principal、scheme 和 expiry token hint。
Threading / Cancellation: 状态更新可来自后台；订阅通知必须按声明调度策略执行。
Diagnostics: authentication diagnostics 必须包含 old state、new state 和 reason。
Tests: `AuthenticationStateTests`
Required Assertions: 断言 snapshot 不可变、状态切换、订阅通知、重复设置和 logout。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-002 Current Principal Access

Feature ID: `AUC-SECURITY-002`
Status: Completed
Goal: 为业务代码提供当前 principal 的同步读取边界。
Public Contract: ICurrentPrincipalAccessor, SecurityPrincipals
Runtime / Build Behavior: 读取返回当前 snapshot 中 principal；无用户时返回 anonymous principal；`SecurityPrincipals.Anonymous` 每次返回独立 unauthenticated principal。
Failure Behavior: principal 缺失、claims 格式异常按 anonymous 或 failed result 处理，不能抛随机异常。
Threading / Cancellation: 读取必须无阻塞；后台线程读取看到一致 snapshot。
Diagnostics: principal diagnostics 必须包含 principal kind 和 missing claim。
Tests: `AuthenticationStateTests`
Required Assertions: 断言 authenticated、anonymous、claims 读取和并发 snapshot。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-003 Permission Registry and Checker

Feature ID: `AUC-SECURITY-003`
Status: Completed
Goal: 注册权限定义并提供权限检查。
Public Contract: PermissionRegistry, IPermissionChecker, PermissionDescriptor
Runtime / Build Behavior: 权限按 stable name 注册；插件权限带 contribution id；checker 只返回 AuthorizationResult，不执行 UI 或导航。
Failure Behavior: 未注册权限返回 Failed/PermissionNotFound；重复权限返回注册失败；贡献撤销后，同一 contribution id 的新权限注册必须被拒绝。
Threading / Cancellation: registry 读并发安全；插件卸载通过 contribution id 撤销权限；checker 必须观察取消 token。
Diagnostics: permission diagnostics 必须包含 permission name、owner 和 principal id。
Tests: `PermissionRegistryTests; PermissionCheckerTests`
Required Assertions: 断言注册、重复、未注册、插件撤销和 checker result。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-004 Authorization Policy Evaluation

Feature ID: `AUC-SECURITY-004`
Status: Completed
Goal: 把 requirement、permission、principal 和 resource 汇总为授权结果。
Public Contract: AuthorizationEvaluator, AuthorizationPolicy, AuthorizationRequirement
Runtime / Build Behavior: evaluator 可直接评估 AuthorizationRequest，也可按 policy name 从 IAuthorizationPolicyProvider 读取 policy 后顺序评估 requirement 并输出 AuthorizationResult。
Failure Behavior: policy 缺失返回 Failed/PolicyNotFound；requirement 不满足返回 Challenge 或 Forbidden；provider 抛异常映射为 Failed/EvaluatorFailed。
Threading / Cancellation: 评估可以异步取消；预取消不调用 provider；取消返回 Cancelled，不触发导航。
Diagnostics: authorization diagnostics 必须包含 policy name、requirement kind 和 resource。
Tests: `AuthorizationEvaluatorTests; AuthorizationPolicyTests`
Required Assertions: 断言成功、拒绝、失败、取消、多 requirement 和 provider 异常。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-005 Route Authorization Guard

Feature ID: `AUC-SECURITY-005`
Status: Completed
Goal: 把 Security 授权接入 Routing guard，但不执行导航本身。
Public Contract: SecurityRouteGuard, SecurityRouteGuardOptions, IRouteAuthorizationPolicyProvider
Runtime / Build Behavior: guard 从 route metadata 获取 policy，调用 evaluator，返回 allow/reject/redirect/cancel/failed result；配置 LoginRouteId 后 Challenge 映射为 redirect hint。
Failure Behavior: 无权限返回 Forbidden reject；未登录默认返回 AuthenticationRequired reject，配置 login route 后返回 redirect；provider 或 evaluator 未声明异常映射为 guard failed。
Threading / Cancellation: guard 必须观察导航 token；取消后不继续评估。
Diagnostics: route auth diagnostics 必须包含 route id、policy name 和 result status。
Tests: `RouteAuthorizationGuardTests`
Required Assertions: 断言 allow、deny、redirect login、取消和 Routing 无 Security 反向依赖。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-006 Command Authorization

Feature ID: `AUC-SECURITY-006`
Status: Ready to Start Product Implementation
Goal: 把权限变化映射到命令可用性和未授权行为。
Public Contract: CommandAuthorizationSource, CommandAuthorizationDescriptor
Runtime / Build Behavior: 命令声明 policy/permission；Security 发布 authorization state，Presentation/MVVM 决定禁用或隐藏。
Failure Behavior: descriptor 缺失、权限撤销、用户状态变化必须触发 CommandAuthorizationChanged。
Threading / Cancellation: 状态变更可来自后台；UI 更新由 Presentation dispatcher 处理。
Diagnostics: command auth diagnostics 必须包含 command id、policy 和 change reason。
Tests: `CommandAuthorizationSourceTests`
Required Assertions: 断言状态变化、禁用/隐藏策略、订阅释放和权限撤销。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-SECURITY-007 Access Token Provider

Feature ID: `AUC-SECURITY-007`
Status: Ready to Start Product Implementation
Goal: 为 Data 等模块提供 token 获取合同。
Public Contract: IAccessTokenProvider, AccessTokenResult, DelegateAccessTokenProvider
Runtime / Build Behavior: token provider 返回 Success/Unavailable/Failed/Cancelled；Data 在 transport 前调用。
Failure Behavior: token 缺失或刷新失败返回 AccessTokenResult，不抛未声明异常。
Threading / Cancellation: 获取 token 必须支持 CancellationToken；并发 refresh 由 provider 合并或明确拒绝。
Diagnostics: token diagnostics 必须包含 request scope、status 和 expiry hint。
Tests: `SecurityRegistrationTests`
Required Assertions: 断言成功、失败、不可用、取消、DI 默认 provider 和 Data 集成前置条件。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
