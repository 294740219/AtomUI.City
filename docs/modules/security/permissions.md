# AtomUI.City.Security Permissions 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Permissions` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Security Permissions 设计

适用范围：权限点声明、命名、权限贡献、插件权限撤销、本地化、AOT/source generator 和诊断。

### 1. 定位

Permission 是框架可识别的稳定权限点。

Permission 不是业务角色，不是用户权限表，也不是 UI 菜单项。它只表达一个可被授权系统检查的能力标识。

### 2. Permission Descriptor

Permission descriptor 建议包含：

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

模块和插件通过 Contribution 提交权限声明：

```text
Module / Plugin
-> PermissionContribution
-> SecurityContributionRegistry
-> PermissionManifest
```

规则：

- 插件不能直接写权限 registry。
- 插件权限必须有 ContributionLease。
- 插件停用时撤销对应权限和 policy metadata。
- 已撤销权限不能再被 Route、Command 或 Data 使用。
- 活动授权缓存必须按 contribution revision 失效。

### 5. 本地化

Permission descriptor 不直接存储显示文本，只存本地化 key。

Localization 负责资源查找和文化切换。Security 只保存 key 和 metadata。

### 6. Source Generator

Security generator 负责：

- 生成 permission manifest。
- 生成 permission descriptor 注册代码。
- 诊断重复权限名。
- 诊断未声明权限引用。
- 诊断插件覆盖 Host 权限。
- 诊断权限名不符合规范。

运行时默认不扫描程序集找权限。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| 重复权限名 | 构建期诊断。 |
| 未声明权限引用 | 构建期诊断；动态场景运行时 Failed。 |
| 插件权限撤销失败 | 聚合错误，继续撤销其他权限。 |
| 权限本地化缺失 | 使用权限名 fallback，并记录诊断。 |

### 8. 测试策略

测试必须覆盖：

- Host 权限注册。
- 插件权限注册和撤销。
- 重复权限诊断。
- 未声明权限引用诊断。
- contribution revision 变化后缓存失效。
