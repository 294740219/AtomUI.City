# AtomUI.City.Security Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。
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

## AtomUI.City.Security Diagnostics and Testing 设计

适用范围：认证诊断、授权诊断、错误策略、测试替身和集成测试工具。

### 1. 定位

Security 必须能解释每一次认证和授权结果。授权失败不能只表现为按钮不可用或导航没发生。

Diagnostics and Testing 子模块负责定义诊断字段、错误分类和测试工具。

### 2. 诊断字段

当前诊断 code、severity 和 required context 以 [diagnostics.md](diagnostics.md) 的逐项表格为唯一合同。以下字段是未来认证 orchestration/持久化 Feature 的候选上下文，不表示当前每条诊断都已包含：

- AuthenticationState。
- Principal id 或匿名标记。
- Scheme。
- Token expiry。
- Refresh attempt id。
- Failure reason。
- ScopeId。
- ContributionId。

未来授权集成可按实际 Feature 增加：

- Authorization result。
- Permission name。
- Policy name。
- Requirement name。
- Resource type。
- RouteId / CommandId / DataClientId。
- PluginId。
- ContributionId。
- Principal revision。
- Policy provider/manifest revision（取决于实际 Feature）。

敏感信息不能写入日志，例如 access token、refresh token、密码、完整 credential。

### 3. 错误分类

当前公开 `SecurityFailureKind` 分类：

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

其中 `ContributionRevoked` 和 `CapabilityDenied` 是未来 PluginSystem 集成保留值，当前评估路径不产生。

### 4. ErrorPolicy 集成

当前 Security 不依赖独立 ErrorPolicy 类型。预期失败通过 `AuthorizationResult`、`RouteGuardResult` 或 `AccessTokenResult` 返回，框架异常同时写入 Core `IHostDiagnostics`。下面的统一 ErrorPolicy 接入是未来跨模块目标。

Security 错误处理规则：

- 授权不通过不是 fatal error。
- Policy/evaluator 异常返回明确 Failed 并写入 `IHostDiagnostics`；当前不接入 ErrorPolicy。
- 具体认证 provider 的 refresh 失败不应直接杀死应用。
- 跨 provider 的插件撤销聚合属于未来 PluginSystem orchestration；当前 provider 独立返回 bool/count。
- 敏感信息必须脱敏。

### 5. Testing 包

当前验证位于 `tests/AtomUI.City.Security.Tests`，仓库没有 Security 专属 Testing helper 包。以下 helper 是未来候选能力，必须先分配 Feature ID：

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

命名和能力范围以未来 Feature 设计为准，不计入 `AUC-SECURITY-001~007` 当前验收。

### 6. 测试场景

`AUC-SECURITY-001~007` 当前必须覆盖：

- anonymous / authenticated / expired / signed out。
- authentication snapshot 成功变更、幂等、非法输入和观察者失败。
- Route allow / reject / redirect / cancel / failed。
- Command `CanExecute` 随权限变化刷新。
- AccessTokenResult 到 Data credential 的当前映射。
- contribution 注册、冲突、撤销和 tombstone。
- policy snapshot、取消边界、诊断失败隔离和订阅生命周期。

具体登录协议、refresh 并发合并、Data 401/403 transport、Capability deny、Source Generator 重复权限和未声明引用诊断属于对应未来 Feature，不计入当前 Completed 验收。

### 7. 无 UI 测试

Security 测试必须能在无真实 AtomUI/Avalonia UI 的环境中运行。

Presentation 相关行为只验证 Security 输出：

- Challenge result。
- Forbidden result。
- Command disabled state。
- Diagnostic record。

不验证具体 UI 控件样式。
