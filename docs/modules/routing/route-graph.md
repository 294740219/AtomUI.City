# AtomUI.City.Routing Route Graph 合同

## 适用范围

本专题属于 `AtomUI.City.Routing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Route Graph` 相关实现决策，不重新定义模块边界。

## 设计决策

- Routing 只负责 Route -> ViewModel Target。
- 参数绑定失败必须返回导航失败结果。
- 插件路由撤销后 route graph 必须重新发布。

## Public Contract

- 只允许通过 `AtomUI.City.Routing` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-ROUTING-001 | Route Template Syntax | RouteTemplateTests; RoutingParameterBoundaryTests |
| AUC-ROUTING-002 | Route Definition Attributes | RouteDefinitionAttributeTests |
| AUC-ROUTING-003 | Route Graph | RouteGraphAndMatcherTests |
| AUC-ROUTING-004 | Route Matcher | RouteGraphAndMatcherTests |
| AUC-ROUTING-005 | Navigation Scope | NavigationScopeTests |
| AUC-ROUTING-006 | Guards | RouteGuardTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Routing Route Graph 设计

适用范围：RouteDescriptor、RouteRegistry、RouteGraphSnapshot、路由贡献、优先级、冲突检测和插件动态变更。

### 1. 定位

Route Graph 表示应用当前可导航结构。

它不是简单列表，而是一棵由 Host、模块和插件共同贡献的不可变快照。导航匹配、Deep Link 解析、扩展点挂载、插件撤销和诊断都依赖 Route Graph。

### 2. 输入来源

Route Graph 输入来自：

- 应用启动期静态模块。
- 模块 Route Map 生成的 manifest。
- 插件 Route Map 生成的 manifest。
- Host 显式开放的 RouteExtensionPoint。
- Redirect route。

所有输入都必须转换为 `RouteContribution`，再进入 `RouteRegistry`。

### 3. RouteDescriptor

`RouteDescriptor` 是运行时消费的路由描述。

建议字段：

| 字段 | 职责 |
|---|---|
| `RouteId` | 稳定身份。 |
| `Template` | 路径模板。 |
| `ParentRouteId` | 父路由。 |
| `OutletName` | 目标 Outlet。 |
| `Kind` | Route、Layout、Index、Group、Redirect、ExtensionPoint。 |
| `ViewModelTarget` | ViewModel 目标描述。 |
| `Parameters` | 参数绑定描述。 |
| `Guards` | Guard descriptor。 |
| `Resolvers` | Resolver descriptor。 |
| `Middleware` | Middleware descriptor。 |
| `Metadata` | 标题、本地化 key、排序、能力等元数据。 |
| `Contribution` | 来源贡献。 |
| `ReusePolicy` | 复用策略。 |

RouteDescriptor 必须不可变。

### 4. RouteRegistry

`RouteRegistry` 负责接收贡献并发布快照。

流程：

```text
Accept RouteContribution
-> Validate contribution
-> Merge with active contributions
-> Build RouteGraphSnapshot
-> Publish snapshot
-> Return ContributionLease
```

规则：

- Registry 不能直接持有插件私有实例。
- Registry 只能保存 descriptor 和 service context 引用。
- ContributionLease 撤销时必须重建快照。
- 快照发布必须原子化。
- 快照版本单调递增。

### 5. RouteGraphSnapshot

`RouteGraphSnapshot` 是不可变结构。

应包含：

- Snapshot id。
- Version。
- RouteDescriptor collection。
- RouteId 索引。
- Parent/children 索引。
- ExtensionPoint 索引。
- Path matcher。
- Redirect 索引。
- Contribution 索引。
- Diagnostics summary。

导航开始时捕获一个 snapshot。本次导航不得访问全局 mutable graph。

### 6. 层级规则

父子关系必须显式。

规则：

- ParentRouteId 必须存在，除非是 root route。
- IndexRoute 必须有 Parent。
- IndexRoute 不能有路径模板。
- RouteGroup 可以没有 ViewModelTarget。
- LayoutRoute 可以有 ViewModelTarget 和子 Outlet。
- RedirectRoute 不能有 ViewModelTarget。
- ExtensionPoint 不能直接被导航进入。
- 插件路由只能挂到 ExtensionPoint 或 Host 显式允许的父节点。

### 7. Path 匹配

Path Template 兼容 ASP.NET Core 10 Route Template 的主要语义。

匹配优先级建议：

1. Literal segment。
2. Constrained parameter。
3. Parameter。
4. Optional parameter。
5. Catch-all。

同级路由如果优先级相同且可能匹配同一路径，Source Generator 或 RouteRegistry 必须报冲突。

运行时不做模糊选择。

### 8. ExtensionPoint

扩展点是 Host 或模块开放给后续模块和插件的挂载位置。

ExtensionPoint 应声明：

- ExtensionPoint id。
- 所属 route。
- 允许 Outlet。
- 允许贡献类型。
- 默认排序规则。
- 能力要求。
- 是否允许插件贡献。

插件贡献到扩展点时，RouteRegistry 必须校验这些规则。

### 9. Route Metadata

Route Metadata 只表达框架级导航信息，不表达业务流程。

推荐元数据：

- Title key。
- Icon key。
- Order。
- Required capability。
- Requires authentication。
- Journal policy。
- Reuse policy。
- Preload policy。
- Diagnostics tags。

权限检查由 Security 和 Guard 承接，Routing 只保存需要交给 Guard 的 metadata。

### 10. Contribution 归属

每个 descriptor 必须记录来源。

```text
RouteDescriptor
  ContributionId
  ModuleId
  PluginId?
  ServiceContext
  Lease
```

用途：

- 插件停用时反查路由。
- RouteScope 创建时选择服务来源。
- 诊断显示来源。
- Journal 清理插件路由。
- 缓存驱逐插件 ViewModel。

### 11. 冲突检测

必须检测：

- RouteId 重复。
- ExtensionPoint id 重复。
- 同级路径冲突。
- Parent 不存在。
- Redirect 目标不存在。
- 静态 Redirect 循环。
- 插件挂载未开放父节点。
- Outlet 名称非法。
- 参数约束不兼容。
- ViewModelTarget 缺失。

冲突默认阻止贡献生效。插件贡献冲突不应影响 Host 已运行路由图。

### 12. 快照更新策略

静态模块启动期贡献失败，默认视为应用启动失败。

插件贡献失败，默认只禁用当前插件贡献并记录诊断。

快照更新顺序：

```text
Build candidate graph
-> Validate candidate graph
-> Atomically swap snapshot
-> Notify observers
-> Store old snapshot for diagnostics window
```

旧 snapshot 可能被正在执行的导航持有。Registry 不能提前释放旧 snapshot 引用的必要 descriptor。

### 13. 测试要求

测试必须覆盖：

- 静态 route graph 构建。
- Parent/children 索引。
- Path 匹配优先级。
- 重复 RouteId 诊断。
- 路径冲突诊断。
- ExtensionPoint 挂载规则。
- 插件贡献和撤销。
- Snapshot version 单调递增。
- 正在导航时 graph 更新不影响当前事务。
