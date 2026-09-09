# AtomUI.City.Security Permissions 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Permissions` 相关实现决策，不重新定义模块边界。

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
- 通过 contribution id 撤销权限后，同一 contribution id 不能在当前 registry 中重新注册权限。

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

## AtomUI.City.Security Permissions 设计

适用范围：权限点声明、命名、权限贡献、插件权限撤销、本地化、AOT/source generator 和诊断。

### 1. 定位

Permission 是框架可识别的稳定权限点。

Permission 不是业务角色，不是用户权限表，也不是 UI 菜单项。它只表达一个可被授权系统检查的能力标识。

### 2. Permission Descriptor

`PermissionDescriptor` 包含：

| 字段 | 说明 |
|---|---|
| Name | 稳定权限名。 |
| DisplayNameKey | 本地化显示文本 key。 |
| DescriptionKey | 本地化描述 key。 |
| Category | 权限分类。 |
| Contribution | 来源模块或插件。 |
| DefaultPolicy | 默认授权策略引用。 |
| IsHostOnly | 是否仅 Host 可授予。 |

权限名必须稳定，不应使用运行时随机值。

`DefaultPolicy` 和 `IsHostOnly` 当前是描述性 metadata；`PermissionRegistry` 与 `PermissionChecker` 不会自动执行这两个字段。需要强制 Host-only 或默认 policy 的应用必须在 composition/policy 层显式实现，并在未来新增框架执行机制时分配 Feature ID。

当前 `PermissionChecker` 使用 `SecurityClaimTypes.Permission` 指定的 claim type（值为 `permission`）判断主体是否持有权限。应用不应复制该字符串常量。

### 3. 命名规则

建议使用小写点分命名：

```text
settings.read
settings.write
project.build
plugin.sales.export
```

规则：

- Host 内置权限不能被插件覆盖。
- 插件权限建议带插件或模块命名空间前缀。
- 权限名大小写敏感策略在实现前统一确定，文档默认按大小写敏感处理。
- 权限名一旦发布，应避免破坏性重命名。

### 4. 权限贡献

当前 Host/应用通过 `PermissionRegistry.Add` 显式提交权限声明，并可附带 contribution id：

```text
Host / application composition
-> PermissionDescriptor
-> PermissionRegistry
```

规则：

- 当前 Security 尚未提供 `ContributionLease` 或 capability gate；插件代码不能仅凭本模块获得隔离保证。
- Host/未来 PluginSystem 停用 contribution 时调用各 registry/provider 的 `RemoveByContribution`。
- 已撤销权限不能再被 Route、Command 或 Data 使用。
- 同一 contribution id 撤销后不能继续向 registry 追加新权限。
- 当前 evaluator 不缓存授权结果；未来新增缓存 Feature 时必须按 contribution revision 失效。

### 5. 本地化

Permission descriptor 不直接存储显示文本，只存本地化 key。

Localization 负责资源查找和文化切换。Security 只保存 key 和 metadata。

### 6. Source Generator

当前没有 Security permission generator、permission manifest schema 或相关 analyzer 诊断。运行时使用显式 registry 注册，也不扫描程序集。

Generator 属于未来候选 Feature；施工前必须先分配 Feature ID，并定义 generated registration、重复/未声明/Host 覆盖诊断及 AOT 测试。通用 `GeneratorFeature.Security` 枚举值本身不代表这些能力已实现。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| 重复权限名 | 当前运行时 Add 返回 false；未来 generator 可增加构建期诊断。 |
| 未声明权限引用 | 当前运行时 Failed/PermissionNotFound；未来 generator 可增加构建期诊断。 |
| 当前 registry 按 contribution 撤销 | 返回删除数量；重复撤销返回 0，并永久拒绝该 contribution 在当前实例中重新注册。 |
| 跨 registry/provider 的插件撤销失败 | 属于未来 PluginSystem orchestration；必须聚合错误并继续撤销其他贡献。 |
| 权限本地化缺失 | 由 Localization/应用决定 fallback 和诊断；Security 只保存资源 key。 |

### 8. 测试策略

测试必须覆盖：

- Host 权限注册。
- 插件权限注册和撤销。
- 重复权限运行时拒绝。
- 未声明权限返回 PermissionNotFound。
- contribution 撤销后 registry 查询和后续授权立即反映最新定义。
