# AtomUI.City.Routing Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Routing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- Routing 只负责 Route -> ViewModel Target。
- 参数绑定失败必须返回导航失败结果。
- 插件路由撤销后 route graph 必须重新发布。
- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

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
| AUC-ROUTING-001 | Route Definition Syntax | RouteTemplateTests; RouteDefinitionAttributeTests |
| AUC-ROUTING-002 | Route Graph Build and Snapshot | RouteGraphAndMatcherTests |
| AUC-ROUTING-003 | Route Matching and Parameters | RouteGraphAndMatcherTests; RoutingParameterBoundaryTests |
| AUC-ROUTING-004 | Navigation Transaction | NavigationScopeTests |
| AUC-ROUTING-005 | Guard and Redirect Pipeline | RouteGuardTests |
| AUC-ROUTING-006 | ViewModel Target Resolution | RouteGraphAndMatcherTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Routing Diagnostics and Testing 设计

适用范围：Routing 诊断事件、日志、错误模型、测试工具、断言能力和无 UI 测试策略。

### 1. 定位

Routing 必须可诊断、可测试。导航失败不能只表现为页面没变化，必须能说明失败发生在哪个阶段、哪个路由、哪个 Guard、哪个 Resolver 或哪个插件贡献。

### 2. 诊断目标

诊断需要服务：

- 开发时调试。
- 自动化测试。
- 插件卸载排查。
- 现场问题定位。
- 性能分析。
- 用户可理解错误提示。

### 3. Navigation Diagnostic Record

每次导航应产生诊断记录。

字段：

| 字段 | 说明 |
|---|---|
| `NavigationId` | 导航唯一标识。 |
| `NavigationScopeId` | 所属 NavigationScope。 |
| `RouteGraphVersion` | 捕获的路由图版本。 |
| `Target` | 目标 RouteId 或 path。 |
| `Source` | RouteReference、Path、Journal、Redirect 等。 |
| `MatchedRoutes` | 匹配路由链。 |
| `Plan` | 保留、离开、新增分支。 |
| `Result` | 成功、拒绝、取消、失败等。 |
| `FailedStage` | 失败阶段。 |
| `Elapsed` | 总耗时。 |
| `Contribution` | 相关贡献。 |

### 4. Stage Diagnostic

每个阶段记录：

- 阶段名称。
- 开始时间。
- 结束时间。
- 耗时。
- 输入摘要。
- 输出结果。
- 取消状态。
- 错误。

阶段包括：

```text
NormalizeTarget
MatchRoute
BuildPlan
RunEnterGuards
ConfirmLeave
ResolveData
CreateViewModel
PrepareCommit
Commit
UpdateJournal
DisposeRemovedBranches
```

### 5. Guard / Resolver 诊断

Guard 记录：

- Guard 类型。
- RouteId。
- 结果。
- Redirect 目标。
- 耗时。

Resolver 记录：

- Resolver 类型。
- RouteId。
- 数据 key。
- 结果。
- Data request correlation id。
- 耗时。

### 6. Plugin 诊断

插件相关诊断必须包含：

- PluginId。
- Plugin version。
- ContributionId。
- Route manifest version。
- Load context id。
- 活动 RouteScope 数量。
- Journal 清理数量。
- Cache 驱逐数量。
- 未释放引用线索。

### 7. 错误模型

Routing 错误应分层：

| 类型 | 示例 |
|---|---|
| Definition error | RouteId 重复、路径冲突。 |
| Matching error | 无匹配、参数不合法。 |
| Policy error | Guard 拒绝、权限不足。 |
| Resolve error | 数据不存在、请求失败。 |
| Activation error | ViewModel 创建或激活失败。 |
| Commit error | Presentation 提交失败。 |
| Plugin error | Contribution 已撤销、插件停用。 |

错误必须可以映射到用户提示，也可以保留开发诊断详情。

### 8. EventBus 边界

Routing 可以发布只读事实事件：

- NavigationStarted。
- NavigationCompleted。
- NavigationCancelled。
- NavigationFailed。
- RouteGraphChanged。

这些事件只用于观察。不能通过 EventBus 控制导航阶段，也不能替代 Navigation Middleware。

### 9. 测试工具

Testing 包应提供：

- `TestRouteGraphBuilder`。
- `TestRouter`。
- `TestNavigationScope`。
- `FakeUiDispatcher`。
- `FakePresentationCommitter`。
- `NavigationRecorder`。
- `RouteGraphAssertions`。
- `JournalAssertions`。
- `PluginRouteTestHost`。

测试工具不依赖真实 AtomUI/Avalonia UI。

### 10. 核心测试场景

必须支持测试：

- RouteReference 格式化和解析。
- RouteGraph 构建。
- Path 匹配。
- Guard 拒绝。
- Resolver 成功和失败。
- Commit 成功和失败。
- 回滚。
- Journal。
- Reuse。
- 插件贡献和撤销。
- 并发导航。
- NavigationScope 停止。

### 11. Deterministic Dispatcher

测试中需要 deterministic dispatcher。

能力：

- 手动推进 UI queue。
- 手动推进后台 queue。
- 控制 Commit 时机。
- 模拟 Commit 中新导航。
- 捕获跨线程违规。

这可以避免路由测试依赖真实线程调度。

### 12. 断言要求

测试断言应能表达：

- 当前 RouteId。
- 当前参数。
- 当前活动 RouteScope。
- 当前 Journal stack。
- 当前 RouteGraph version。
- 已释放 RouteScope。
- Guard/Resolver 执行顺序。
- 插件 Contribution 是否清理。
- 是否没有未释放 Operation。

### 13. 性能指标

Routing 诊断应能统计：

- RouteGraph 构建耗时。
- Path 匹配耗时。
- Guard 总耗时。
- Resolver 总耗时。
- Commit 耗时。
- Journal 写入耗时。
- RouteScope 释放耗时。

性能指标不能强依赖外部 telemetry 系统。Core Diagnostics 先提供统一出口。

### 14. 文档完成标准

Routing 实现前，对应测试设计必须覆盖：

- 成功路径。
- 拒绝路径。
- 取消路径。
- 失败路径。
- 插件撤销路径。
- 线程调度路径。
- 回滚路径。

这些测试不是后补项，而是 Routing 编程模型的一部分。
