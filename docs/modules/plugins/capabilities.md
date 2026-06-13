# AtomUI.City.PluginSystem Capabilities 合同

## 适用范围

本专题属于 `AtomUI.City.PluginSystem` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Capabilities` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。

## Public Contract

- 只允许通过 `AtomUI.City.PluginSystem` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-PLUGIN-001 | Plugin Metadata | PluginDeclarationAttributeTests; PluginManifestTests |
| AUC-PLUGIN-002 | Dependency Validation | PluginDependencyTests |
| AUC-PLUGIN-003 | Package Installation | PluginPackageTests |
| AUC-PLUGIN-004 | Discovery | PluginLoadingTests |
| AUC-PLUGIN-005 | Loading | PluginLoadingTests |
| AUC-PLUGIN-006 | MSBuild Contract | PluginMsBuildContractTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## PluginSystem 能力授权设计

适用范围：插件能力声明、授权、能力范围、Contribution 校验和诊断

### 1. 目标

能力系统用于约束插件可以向 Host 增加什么能力。它不是安全沙箱，但它是 Host 策略、用户授权和诊断的基础。

设计目标：

- 插件在清单中声明 requested capabilities。
- Host policy 产生 granted capabilities。
- Contribution 必须在授权能力范围内。
- 能力范围可表达路由、数据客户端、事件、后台任务等边界。
- 能力授权结果可审计、可撤销。

### 2. requested 和 granted

插件清单声明请求能力：

```json
{
  "capabilities": [
    {
      "name": "routes",
      "scope": ["/sales/**"]
    },
    {
      "name": "data.http",
      "clients": ["SalesApi"]
    }
  ]
}
```

Host 评估后生成授权能力：

```json
{
  "grantedCapabilities": [
    {
      "name": "routes",
      "scope": ["/sales/**"]
    }
  ]
}
```

规则：

- requested capability 不等于 granted capability。
- 未授权能力不能产生 Contribution。
- 授权结果必须写入锁定文件和安装记录。
- 能力变更可能要求插件重新启用。

### 3. 能力目录

第一版建议能力：

| 能力 | 范围 |
|---|---|
| `modules` | 插件模块声明。 |
| `services` | 插件私有服务和受控 contract 服务。 |
| `routes` | 路由 path pattern。 |
| `presentation.views` | View/ViewModel 映射。 |
| `presentation.resources` | 样式、图标、菜单、工具栏资源。 |
| `commands` | 命令和动作入口。 |
| `permissions` | 权限点声明。 |
| `localization` | 本地化资源。 |
| `eventbus.subscribe` | 可订阅事件 contract。 |
| `eventbus.publish` | 可发布事件 contract。 |
| `data.http` | HTTP client 名称。 |
| `data.grpc` | gRPC client 名称。 |
| `data.signalr` | SignalR connection 名称。 |
| `background.tasks` | 后台任务类型或数量限制。 |
| `settings` | 设置页面和配置 section。 |
| `diagnostics` | 诊断 provider。 |

### 4. 范围表达

能力范围必须尽可能具体。

示例：

```json
{
  "name": "eventbus.subscribe",
  "contracts": [
    "Company.Contracts.SalesOrderChanged"
  ]
}
```

```json
{
  "name": "routes",
  "scope": [
    "/sales/**",
    "/reports/sales"
  ]
}
```

规则：

- 路由能力不能使用全局通配，除非 Host 显式授权。
- Data 能力必须声明 client 名称。
- EventBus 能力必须声明共享 contract。
- 后台任务能力必须绑定生命周期和取消策略。

### 5. 授权流程

```text
Read requested capabilities
-> Check package trust
-> Check Host policy
-> Check user/admin consent
-> Produce granted capabilities
-> Validate contribution manifests
-> Apply contributions with lease
```

规则：

- 能力授权发生在 Contribution 应用前。
- 授权失败不一定阻止插件安装。
- 必需能力被拒绝时，插件不能启用。
- 可选能力被拒绝时，插件可以降级启用。

### 6. Contribution 校验

每个 Contribution 必须校验来源和能力。

校验输入：

- PluginId。
- ModuleId。
- ContributionId。
- Contribution type。
- requested capabilities。
- granted capabilities。
- Host registry policy。

校验失败时，不创建 lease。

### 7. 能力撤销

能力撤销流程：

```text
Update granted capabilities
-> Stop new plugin entry
-> Revoke affected contribution leases
-> Cancel affected operations
-> Mark plugin degraded or inactive
```

规则：

- 能力撤销不能留下已生效 Contribution。
- 正在运行的 Operation 必须收到取消。
- UI 入口应禁用或移除。
- 诊断必须记录撤销原因。

### 8. 测试要求

必须覆盖：

- 未授权能力被拒绝。
- 必需能力被拒绝导致插件不能启用。
- 可选能力被拒绝后降级启用。
- Contribution 超出 routes 范围。
- EventBus 使用未声明 contract。
- Data client 未授权。
- 能力撤销后 lease 被撤销。
