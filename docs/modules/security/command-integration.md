# AtomUI.City.Security Command Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Command Integration` 相关实现决策，不重新定义模块边界。

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

Command metadata 可以来自 attribute、builder API 或 Source Generator manifest。

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
| Policy 抛异常 | `CanExecute = false`，记录诊断。 |
| 插件 command 权限撤销 | Command contribution disabled 或 removed。 |
| 登录态未知 | 默认不可执行，除非 command 标记匿名可执行。 |

### 8. 测试策略

测试必须覆盖：

- 权限变化刷新 `CanExecute`。
- 登录态变化刷新 `CanExecute`。
- 执行前二次授权。
- CompositeCommand 子命令授权变化。
- 插件 command 撤销。
- Presentation 不直接参与授权判断。
