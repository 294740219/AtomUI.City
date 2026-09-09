# AtomUI.City.Security Route Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Route Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- Routing 只负责 Route -> ViewModel Target。
- 参数绑定失败必须返回导航失败结果。
- 插件路由撤销后 route graph 必须重新发布。
- 授权失败返回明确 result，不能直接操作 UI。
- 当前权限声明来自 registry；plugin capability 属于未来 PluginSystem 集成。
- 认证状态通过 `IAuthenticationStateProvider.StateChanged` 发布；当前只有 Command source 直接订阅，Route/Data 在每次操作中读取当前 Security contract，其他联动由应用 bridge 负责。

## Public Contract

- 只允许通过 `AtomUI.City.Security` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- Owner 必须明确：Host、Module、Plugin、Route、Operation、Connection、View 或 Test scope。
- 释放必须幂等；释放后 mutating API 必须失败或返回声明的 Result。
- Cancellation 必须在进入外部调用、用户 handler、插件代码、IO、dispatcher work 前后观察。
- 插件来源对象必须可撤销，不能泄漏到 Host 根单例。

## 失败行为

- 输入无效：使用标准参数异常或模块 Result。
- 生命周期状态非法：返回失败 Result、模块异常或稳定诊断。
- 依赖缺失：阻止当前功能启用，不影响无关功能。
- 插件卸载中：拒绝创建新贡献，并撤销已有贡献。
- 释放失败：记录诊断并继续释放其他资源。

## 测试要求

| Feature ID | 相关能力 | 测试文件 |
| --- | --- | --- |
| AUC-SECURITY-001 | Authentication State | AuthenticationStateTests |
| AUC-SECURITY-002 | Current Principal | AuthenticationStateTests |
| AUC-SECURITY-003 | Permission Registry and Checker | PermissionRegistryTests; PermissionCheckerTests |
| AUC-SECURITY-004 | Authorization Policy | AuthorizationPolicyTests; AuthorizationEvaluatorTests |
| AUC-SECURITY-005 | Route Guard | RouteAuthorizationGuardTests |
| AUC-SECURITY-006 | Command Authorization | CommandAuthorizationSourceTests |
| AUC-SECURITY-007 | Access Token Provider | SecurityRegistrationTests; AccessTokenCredentialProviderTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Security Route Integration 设计

适用范围：Route 授权元数据、Security Guard、Challenge、Redirect、Reject、Forbidden 和导航诊断。

### 1. 定位

Route integration 负责把路由进入决策接入 Security 授权系统。

Routing 负责执行导航事务和 Guard 顺序。Security 负责解释 route auth metadata 并返回授权结果。

### 2. Route Auth Metadata

当前 Route 授权 metadata 只有 `routeId -> AuthorizationPolicy` 映射，由 `InMemoryRouteAuthorizationPolicyProvider.Add(routeId, policy)` 显式注册。没有 policy 的 route 视为匿名可访问。每个 route 的 fallback 和 challenge metadata、attribute 及 Source Generator manifest 尚未实现；若未来增加，必须单独登记跨模块 Feature。

### 3. Guard 流程

```text
Route matched
-> Routing builds Guard context
-> Security route guard queries policy by RouteId and reads current principal
-> AuthorizationEvaluator evaluates
-> GuardResult returned
-> NavigationTransaction continues / rejects / redirects / cancels / fails
```

Routing 传入 `RouteGuardContext`（route descriptor、参数和 navigation context）；Security 另外通过 `ICurrentPrincipalAccessor` 读取当前 principal，并按 route id 查询 policy。当前合同不从 Route metadata 读取 principal、policy 或 challenge 配置。

参与诊断和授权的上下文包括：

- RouteId。
- Route 参数。
- CancellationToken。

Security 不访问 UI 对象，不创建 ViewModel，不修改 NavigationSnapshot。

### 4. 结果映射

| Authorization result | Guard result | Routing 行为 |
|---|---|---|
| Allowed | Allow | 继续导航。 |
| Challenge | 配置 LoginRouteId 时 Redirect，否则 Reject/AuthenticationRequired | Routing 执行重定向或保持当前页面。 |
| Forbidden | Reject | 拒绝导航，可由 Presentation 展示拒绝访问。 |
| Denied | Reject | 拒绝导航。 |
| Failed | Failed | 导航失败，记录诊断。 |
| Cancelled | Cancel | 导航取消。 |

Redirect 必须由 NavigationTransaction 统一处理，Security Guard 内部不能直接调用 `IRouter`。

### 5. Challenge

Challenge 表示需要认证动作。当前 `SecurityRouteGuardOptions` 只支持配置 `LoginRouteId` 和登录导航选项：已配置时返回 Redirect，否则返回带 `AuthenticationRequired` code 的 Reject。Guard 不触发 Interaction、不刷新 session，也不直接导航；更复杂的 challenge orchestration 属于应用或未来 Feature。

### 6. 插件路由

当前 Security 只支持按 policy `ContributionId` 调用 `RemoveByContribution` 删除 Route policy 并阻止同 contribution 重新注册。下面的 manifest、插件 capability 和私有 requirement 约束属于未来 PluginSystem 集成目标。

未来集成规则：

- 插件 route auth metadata 必须来自插件 manifest 或 source generator descriptor。
- 未来如引入授权缓存，插件停用后必须按 contribution revision 失效。
- 插件不能声明覆盖 Host route 的权限语义。
- 插件私有 requirement 类型不能泄漏到 Host policy contract。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| route 未注册 policy | Allow。 |
| permission 未声明 | Guard failed。 |
| 已注册 policy 的 requirement 无法满足 | 按 evaluator result 映射。 |
| 未登录 | Redirect 或 Reject/AuthenticationRequired。 |
| 权限不足 | Reject / Forbidden。 |
| evaluator 异常 | Guard failed。 |

### 8. 测试策略

测试必须覆盖：

- 匿名路由放行。
- 需要登录路由返回 Redirect 或 Reject/AuthenticationRequired。
- 权限不足返回 Reject。
- Redirect 策略。
- contribution 撤销后下一次 route policy 查询返回最新结果。
- Guard cancellation。
