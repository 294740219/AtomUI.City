# AtomUI.City.Security Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。
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

## AtomUI.City.Security Diagnostics and Testing 设计

适用范围：认证诊断、授权诊断、错误策略、测试替身和集成测试工具。

### 1. 定位

Security 必须能解释每一次认证和授权结果。授权失败不能只表现为按钮不可用或导航没发生。

Diagnostics and Testing 子模块负责定义诊断字段、错误分类和测试工具。

### 2. 诊断字段

认证诊断应包含：

- AuthenticationState。
- Principal id 或匿名标记。
- Scheme。
- Token expiry。
- Refresh attempt id。
- Failure reason。
- ScopeId。
- ContributionId。

授权诊断应包含：

- Authorization result。
- Permission name。
- Policy name。
- Requirement name。
- Resource type。
- RouteId / CommandId / DataClientId。
- PluginId。
- ContributionId。
- Principal revision。
- Policy manifest revision。

敏感信息不能写入日志，例如 access token、refresh token、密码、完整 credential。

### 3. 错误分类

建议分类：

| 分类 | 说明 |
|---|---|
| AuthenticationRequired | 需要登录或重新认证。 |
| AuthenticationExpired | 认证过期。 |
| Forbidden | 已认证但权限不足。 |
| PolicyNotFound | Policy 不存在。 |
| PermissionNotFound | Permission 未声明。 |
| RequirementFailed | Requirement 不满足。 |
| EvaluatorFailed | Evaluator 异常。 |
| ContributionRevoked | 来源贡献已撤销。 |
| CapabilityDenied | 插件 capability 被拒绝。 |

### 4. ErrorPolicy 集成

Security 错误处理规则：

- 授权不通过不是 fatal error。
- Policy/evaluator 异常进入 ErrorPolicy，但返回明确 Failed。
- 认证 refresh 失败不直接杀死应用。
- 插件撤销失败聚合错误并继续清理。
- 敏感信息必须脱敏。

### 5. Testing 包

Testing 包应提供：

- `TestPrincipalBuilder`。
- `FakeAuthenticationStateProvider`。
- `FakeAuthenticationService`。
- `FakeAccessTokenProvider`。
- `FakePermissionChecker`。
- `FakeAuthorizationEvaluator`。
- `RouteAuthorizationTestHost`。
- `CommandAuthorizationTestHost`。
- `DataAuthPipelineTestHost`。
- `PluginSecurityContributionTestHost`。

命名最终以实现阶段 API 规范为准，但能力必须覆盖这些场景。

### 6. 测试场景

必须覆盖：

- anonymous / authenticated / expired / signed out。
- 登录成功、登录取消、登录失败。
- refresh 成功、失败、并发合并。
- Route allow / reject / redirect / challenge。
- Command `CanExecute` 随权限变化刷新。
- Data 401 / 403 映射。
- 插件权限贡献、冲突、撤销。
- Capability deny。
- Source Generator 重复权限诊断。
- 未声明权限引用诊断。

### 7. 无 UI 测试

Security 测试必须能在无真实 AtomUI/Avalonia UI 的环境中运行。

Presentation 相关行为只验证 Security 输出：

- Challenge result。
- Forbidden result。
- Command disabled state。
- Diagnostic record。

不验证具体 UI 控件样式。
