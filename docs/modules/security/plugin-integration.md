# AtomUI.City.Security Plugin Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Security` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Plugin Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。
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

## AtomUI.City.Security Plugin Integration 设计

适用范围：插件权限贡献、Policy requirement、Capability、Host 授权、撤销、缓存失效和 contract 隔离。

### 1. 定位

Plugin integration 负责约束插件如何参与 Security。

插件可以声明权限和授权元数据，但不能成为全局权限解释者。Host Security 是唯一授权决策入口。

### 2. 插件可贡献内容

插件可以贡献：

- Permission descriptor。
- Policy requirement descriptor。
- Route auth metadata。
- Command auth metadata。
- Data client auth metadata。
- Capability request。

所有贡献都必须通过 Contribution Request 和 ContributionLease 进入 Security registry。

### 3. Capability

Capability 表达 Host 允许插件使用的框架能力。

示例：

| Capability | 说明 |
|---|---|
| ContributeRoutes | 允许贡献路由。 |
| ContributeCommands | 允许贡献命令。 |
| UseDataClient | 允许访问指定 Data client。 |
| SubscribeEvents | 允许订阅指定事件 contract。 |
| PublishEvents | 允许发布指定事件 contract。 |
| ContributePresentationResources | 允许贡献 View、Style、Icon 等资源。 |

Capability 不等同于业务权限。Capability 是 Host 对插件能力的授权，Permission 是用户或主体对业务能力的授权。

### 4. Host 授权

插件启用时：

```text
Plugin metadata
-> Capability request
-> Host policy check
-> grant / reject capabilities
-> accept Security contributions
```

规则：

- 未授予 capability 的插件贡献不得进入 registry。
- 插件不能扩大自己的 capability。
- Capability grant 必须可诊断。
- Capability 变化必须触发相关授权缓存失效。

### 5. Contract 隔离

跨插件边界的授权 contract 必须位于 Host 共享 contract 程序集。

禁止：

- Host policy 依赖插件私有 requirement 类型。
- Host 静态缓存持有插件私有授权对象。
- 插件直接修改 Host principal。
- 插件绕过 Security 自行决定全局权限。

### 6. 撤销和卸载

插件停用时：

```text
Stop new Security checks from plugin contributions
-> revoke route / command / data auth metadata
-> revoke permissions and policies
-> invalidate authorization cache
-> recompute active commands and guards
-> dispose contribution leases
```

如果插件还有活动 RouteScope、ActivationScope 或 OperationScope，Host 必须先关闭相关运行实例，再释放插件 Security contribution。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| 插件权限名冲突 | 拒绝该 contribution。 |
| 插件请求未授权 capability | 拒绝对应 contribution。 |
| 插件 requirement 类型泄漏 | 构建期或启用期诊断。 |
| 撤销失败 | 聚合错误，继续撤销其他 contribution。 |
| 卸载后仍有授权缓存引用 | 标记 UnloadPending，输出诊断。 |

### 8. 测试策略

测试必须覆盖：

- 插件权限贡献成功。
- 插件权限冲突被拒绝。
- 未授权 capability 被拒绝。
- 插件停用后权限和缓存撤销。
- Host 不持有插件私有类型引用。
