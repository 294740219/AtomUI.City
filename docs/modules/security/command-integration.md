# AtomUI.City.Security Command Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Command Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

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
| AUC-SECURITY-007 | Access Token Provider | SecurityRegistrationTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Security Command Integration 设计

适用范围：Command 授权元数据、`CanExecute` 联动、权限变化刷新、Presentation 显示策略和测试支持。

### 1. 定位

Command integration 负责把权限检查接入 MVVM Command 的可执行状态。

Mvvm 提供 Command 状态和刷新入口。Security 负责授权判断。Presentation 负责展示按钮、菜单、快捷键等 UI 状态。

### 2. Command Auth Metadata

Command 可以声明：

- 需要登录。
- 需要 permission。
- 需要 policy。
- 未授权时 disabled。
- 未授权时 hidden。
- 未授权提示 key。

当前 Command metadata 通过 `InMemoryCommandAuthorizationDescriptorProvider.Add` 显式注册。descriptor 未显式指定 contribution 时继承 policy contribution；二者同时指定但不一致时构造失败，避免撤销后残留直接持有 policy 的 command。attribute、builder API 和 Source Generator manifest 尚未实现，不能作为当前用法。

### 3. CanExecute 数据流

```text
Authentication / permission state changed
-> Security command authorization source invalidates
-> Command CanExecute recompute
-> Presentation refreshes enabled / visible state
```

Command 可执行状态可以同时受以下因素影响：

- Security 权限。
- Routing 当前状态。
- ViewModel active 状态。
- Validation 状态。
- Operation 正在执行状态。

Security 只提供授权维度，不覆盖其他维度。

当前 Security source 只直接订阅认证状态、command descriptor 和 permission registry。Routing 当前状态、ViewModel active、Validation 和 Operation 状态由 MVVM/应用组合；如需让路由变化刷新 command，应用必须提供 adapter。

### 4. 用户动作

执行命令前必须再次检查授权，不能只依赖 UI disabled 状态。

```text
UI invokes command
-> Command checks active / validation / operation state
-> Security authorization check
-> Execute or return authorization failure
```

这可以避免权限变化后 UI 尚未刷新时执行旧权限命令。

### 5. Presentation 表达

Presentation 可以根据授权结果展示：

- Disabled。
- Hidden。
- Tooltip。
- 权限不足提示。
- 登录提示。

Presentation 不读取权限存储，不解释 Policy，只消费 Command 状态和 Security 结果。

### 6. CompositeCommand

本节描述未来 MVVM 集成目标。Security 当前只发布单个 command id 的授权状态，不提供 `CompositeCommand` 类型或组合命令管理器。

组合命令需要过滤当前 active 上下文中的可执行子命令。

规则：

- 子命令权限变化会触发组合命令状态刷新。
- 当前无授权子命令时组合命令不可执行。
- 插件子命令撤销后必须从组合命令中移除。
- 权限失败应进入 Command diagnostics。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| 授权未通过 | `CanExecute = false`，执行时返回 authorization failure。 |
| Policy 或 descriptor provider 抛异常 | `CanExecute = false`，返回 Failed/EvaluatorFailed，记录诊断。 |
| contribution command 权限撤销 | `RemoveByContribution` 删除 descriptor、发布全量刷新，并阻止同 contribution 重新注册。 |
| command 没有 descriptor | Allowed；注册了受保护 descriptor 时按 policy 评估当前登录态。 |

### 8. 测试策略

测试必须覆盖：

- 权限变化刷新 `CanExecute`。
- 登录态变化刷新 `CanExecute`。
- 执行前二次授权。
- 当前 descriptor contribution 批量撤销和重新注册拒绝。
- CompositeCommand 子命令授权变化在对应 MVVM 集成 Feature 建立后测试。
- 插件 command 撤销。
- 构造期订阅失败回滚已完成订阅；Dispose 尝试释放全部订阅并聚合失败，重复 Dispose 幂等。
- Presentation 不直接参与授权判断。
