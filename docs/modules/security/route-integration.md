# AtomUI.City.Security Route Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Route Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- Routing 只负责 Route -> ViewModel Target。
- 参数绑定失败必须返回导航失败结果。
- 插件路由撤销后 route graph 必须重新发布。
- 授权失败返回明确 result，不能直接操作 UI。
- 权限声明必须来自 registry 或 plugin capability。
- 认证状态变更必须通知 command、route 和 data 集成点。

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
| AUC-SECURITY-002 | Permission Registry | PermissionRegistryTests |
| AUC-SECURITY-003 | Permission Checker | PermissionCheckerTests |
| AUC-SECURITY-004 | Authorization Policy | AuthorizationPolicyTests; AuthorizationEvaluatorTests |
| AUC-SECURITY-005 | Route Guard | RouteAuthorizationGuardTests |
| AUC-SECURITY-006 | Command Authorization | CommandAuthorizationSourceTests |

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

Route 可以声明：

- 需要登录。
- 需要 permission。
- 需要 policy。
- 匿名可访问。
- 授权失败 fallback route。
- Challenge 行为。

Route metadata 必须进入 Route descriptor，由 Routing 的 Source Generator 输出。Security 不在运行时扫描路由类型。

### 3. Guard 流程

```text
Route matched
-> Routing builds Guard context
-> Security route guard reads route auth metadata
-> AuthorizationEvaluator evaluates
-> GuardResult returned
-> NavigationTransaction continues / rejects / redirects / challenges
```

Routing 传给 Security 的上下文：

- RouteId。
- Route metadata。
- 当前 principal。
- Route 参数。
- Contribution 来源。
- Navigation transaction id。
- CancellationToken。

Security 不访问 UI 对象，不创建 ViewModel，不修改 NavigationSnapshot。

### 4. 结果映射

| Authorization result | Guard result | Routing 行为 |
|---|---|---|
| Allowed | Allow | 继续导航。 |
| Challenge | Redirect 或 Reject with challenge | 进入登录流程或保持当前页面。 |
| Forbidden | Reject | 拒绝导航，可由 Presentation 展示拒绝访问。 |
| Denied | Reject | 拒绝导航。 |
| Failed | Failed | 导航失败，记录诊断。 |
| Cancelled | Cancel | 导航取消。 |

Redirect 必须由 NavigationTransaction 统一处理，Security Guard 内部不能直接调用 `IRouter`。

### 5. Challenge

Challenge 表示需要认证动作。

可能行为：

- 跳转登录路由。
- 触发登录 Interaction。
- 尝试 refresh session。
- 返回 rejected 并让应用决定。

具体策略由 Host 配置，不由 Routing 或 Presentation 私自决定。

### 6. 插件路由

插件路由授权必须携带 Contribution 信息。

规则：

- 插件 route auth metadata 必须来自插件 manifest 或 source generator descriptor。
- 插件停用后，该插件路由的授权缓存必须失效。
- 插件不能声明覆盖 Host route 的权限语义。
- 插件私有 requirement 类型不能泄漏到 Host policy contract。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| route auth metadata 无效 | Guard failed。 |
| permission 未声明 | Guard failed。 |
| policy 不存在 | Guard failed。 |
| 未登录 | Challenge。 |
| 权限不足 | Reject / Forbidden。 |
| evaluator 异常 | Guard failed。 |

### 8. 测试策略

测试必须覆盖：

- 匿名路由放行。
- 需要登录路由返回 Challenge。
- 权限不足返回 Reject。
- Redirect 策略。
- 插件路由停用后缓存失效。
- Guard cancellation。
