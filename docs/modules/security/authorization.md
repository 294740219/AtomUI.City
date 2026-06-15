# AtomUI.City.Security Authorization 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Authorization` 相关实现决策，不重新定义模块边界。

## 设计决策

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
| AUC-SECURITY-002 | Current Principal | AuthenticationStateTests |
| AUC-SECURITY-003 | Permission Registry and Checker | PermissionRegistryTests; PermissionCheckerTests |
| AUC-SECURITY-004 | Authorization Policy | AuthorizationPolicyTests; AuthorizationEvaluatorTests |
| AUC-SECURITY-005 | Route Guard | RouteAuthorizationGuardTests |
| AUC-SECURITY-006 | Command Authorization | CommandAuthorizationSourceTests |
| AUC-SECURITY-007 | Access Token Provider | SecurityRegistrationTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Security Authorization 设计

适用范围：Policy、Requirement、授权结果、Challenge、Forbidden、授权执行器和跨模块授权集成。

### 1. 定位

Authorization 子模块负责回答一个问题：当前主体是否允许执行某个受保护动作。

它不定义业务权限模型，不持久化用户权限，不决定 UI 如何展示授权失败。

### 2. 授权输入

授权评估输入包含：

- 当前 `ClaimsPrincipal`。
- Permission name。
- Policy name。
- Resource descriptor。
- Route / Command / Data / Plugin 上下文。
- Contribution 信息。
- CancellationToken。

授权输入必须是可序列化诊断的 descriptor，不应直接包含 AtomUI/Avalonia 控件实例。

### 3. Policy

Policy 是授权规则组合。

典型 requirement：

| Requirement | 说明 |
|---|---|
| Authenticated | 需要登录。 |
| Permission | 需要指定权限点。 |
| Claim | 需要指定 claim。 |
| Role | 需要指定 role claim。 |
| PluginCapability | 需要 Host 授予插件 capability。 |
| CustomRequirement | 应用自定义 requirement。 |

Policy descriptor 必须由显式声明或 Source Generator manifest 提供，运行时默认不扫描程序集。

### 4. 授权结果

授权不返回裸 bool。

建议结果：

```text
Allowed
Denied
Forbidden
Challenge
Failed
Cancelled
```

语义：

| 结果 | 说明 |
|---|---|
| `Allowed` | 授权通过。 |
| `Denied` | 策略不满足，但不区分登录和权限。 |
| `Forbidden` | 已认证，但权限不足。 |
| `Challenge` | 需要登录、刷新或重新认证。 |
| `Failed` | Policy 或 evaluator 自身失败。 |
| `Cancelled` | 授权检查被取消。 |

Route、Command、Data 可以把结果映射成自己的行为，但不能改变 Security 的语义。

### 5. Evaluator

`IAuthorizationEvaluator` 负责执行 policy。

规则：

- 必须支持异步。
- 必须支持取消。
- 可以直接评估 `AuthorizationRequest`，也可以通过 `IAuthorizationPolicyProvider` 按 policy name 读取策略后评估。
- 不能访问 UI 对象。
- 不能阻塞 UI Thread。
- 可以使用缓存，但缓存必须带认证状态 revision 和 contribution revision。
- Policy 异常返回 Failed，并记录诊断。

### 6. Challenge 和 Forbidden

`Challenge` 表示需要认证动作，例如登录或刷新。

`Forbidden` 表示当前主体已认证，但不具备所需权限。

默认映射：

| 结果 | Route | Command | Data | Presentation |
|---|---|---|---|---|
| Challenge | 登录或重定向 | 不可执行 | 401 处理 | 登录交互 |
| Forbidden | 拒绝或拒绝访问页 | 不可执行 | 403 处理 | 权限不足提示 |

Presentation 可以展示 UI，但不能重新解释授权结果。
`SecurityRouteGuard` 默认把 Challenge 映射为 reject；Host 配置 login route 后只返回 Routing redirect result，不直接执行导航。

### 7. 缓存策略

授权结果可以缓存，但必须受以下因素影响：

- Principal revision。
- Permission manifest revision。
- Policy manifest revision。
- Plugin contribution revision。
- Route / Command / resource identity。

用户切换、登录态变化、插件停用、权限贡献撤销都必须让相关缓存失效。

### 8. 错误策略

| 场景 | 默认处理 |
|---|---|
| Policy 不存在 | Failed，并记录 manifest 诊断。 |
| Requirement 未注册 | Failed。 |
| Evaluator 抛异常 | Failed，进入 ErrorPolicy。 |
| 授权取消 | Cancelled。 |
| 插件 requirement 已撤销 | Failed 或 Forbidden，按场景返回。 |

### 9. 测试策略

测试必须覆盖：

- authenticated requirement。
- permission requirement。
- claim / role requirement。
- Challenge 和 Forbidden 区分。
- evaluator 异常。
- 缓存随 principal revision 失效。
- 插件 contribution 撤销后授权重新计算。
