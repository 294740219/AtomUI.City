# AtomUI.City.Routing Journal And Reuse 合同

## 适用范围

本专题属于 `AtomUI.City.Routing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Journal And Reuse` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Routing Journal and Reuse 设计

适用范围：NavigationJournal、Back/Forward、Replace/Reset、路由状态恢复、RouteReusePolicy、KeepAlive 和插件路由清理。

### 1. 定位

Journal 记录 NavigationScope 内的导航历史。Reuse 控制路由分支是否保留或缓存。

两者都必须服务桌面长期运行模型，避免无边界缓存和插件卸载失败。

### 2. NavigationJournal

每个 NavigationScope 拥有独立 Journal。

Journal 记录：

- Back stack。
- Current entry。
- Forward stack。

不跨 NavigationScope 共享。

### 3. JournalEntry

JournalEntry 只保存可恢复导航状态。

建议字段：

| 字段 | 职责 |
|---|---|
| `RouteId` | 目标路由。 |
| `Parameters` | 可序列化参数。 |
| `Query` | Query 参数。 |
| `Fragment` | Fragment。 |
| `RouteGraphVersion` | 创建时图版本。 |
| `ContributionId` | 来源贡献。 |
| `StateSnapshotKey` | 可选状态快照引用。 |
| `Title` | 可选标题快照。 |

JournalEntry 禁止保存：

- ViewModel。
- View。
- ServiceProvider。
- Delegate。
- Stream。
- 插件私有类型实例。

### 4. 导航模式

支持：

| 模式 | 说明 |
|---|---|
| `Push` | 新 entry 入栈。 |
| `Replace` | 替换当前 entry。 |
| `Reset` | 清空历史并设置当前 entry。 |
| `Skip` | 不记录历史。 |

默认普通导航使用 Push。

### 5. Back / Forward

Back 流程：

```text
Read previous JournalEntry
-> Validate entry
-> Navigate with RestoreState
-> Move current to forward stack
```

Forward 类似。

如果 entry 的 route contribution 已撤销：

- 默认跳过该 entry。
- 从 Journal 中清除。
- 记录诊断。
- 继续寻找下一个可用 entry。

### 6. 状态恢复

Journal 不直接保存 ViewModel 状态。

需要恢复时：

- RouteScope 状态通过 StateSnapshot 保存。
- JournalEntry 保存 snapshot key。
- 导航恢复时由 State 模块读取 snapshot。

不可序列化状态不进入 Journal。

### 7. Route Reuse

复用策略分两类：

1. 共同父路由保留。
2. 已离开分支缓存。

共同父路由保留是默认行为。例如从 `settings/profile` 到 `settings/security`，`settings` 布局路由可以保留。

已离开分支缓存默认关闭，必须显式启用。

### 8. RouteReusePolicy

建议策略：

| 策略 | 说明 |
|---|---|
| `DisposeOnLeave` | 默认，离开即释放。 |
| `KeepAliveInNavigationScope` | 在当前 NavigationScope 内缓存。 |
| `KeepAliveUntilMemoryPressure` | 可选，受容量和内存策略约束。 |
| `NeverReuse` | 参数相同也重新创建。 |

缓存必须有容量限制和诊断。

### 9. KeepAlive

KeepAlive 分支必须保留：

- RouteScope。
- ActivationScope 或可重新激活状态。
- ViewModel。
- Resolved data。
- StateScope。

KeepAlive 分支不能保留：

- 已取消 Operation。
- 插件卸载中的资源。
- 无边界后台任务。
- Presentation 不允许保留的 View。

Presentation 可以拒绝某些 View 保留，Routing 必须按结果降级为释放。

### 10. 参数变化

同一 RouteId 参数变化时，策略决定：

- 复用实例并更新 RouteContext。
- 重新运行 Resolver。
- 重建 ViewModel。

默认规则：

- RouteId 相同且参数相同：可保留。
- RouteId 相同但 path 参数变化：重新解析数据。
- RouteId 不同：按路由树 diff 处理。

### 11. 插件清理

插件停用时必须：

```text
Find Journal entries by ContributionId
-> Remove entries
-> Evict reuse cache branches
-> Dispose related RouteScopes
-> Clear snapshot references
```

不允许 Journal 或 Reuse cache 保留插件类型实例，否则插件 AssemblyLoadContext 无法卸载。

### 12. 诊断

必须记录：

- Journal push/replace/reset。
- Back/Forward 目标。
- Entry 无效原因。
- Reuse 命中。
- Reuse 驱逐。
- KeepAlive 容量。
- 插件 entry 清理数量。

### 13. 测试要求

测试必须覆盖：

- Push / Replace / Reset。
- Back / Forward。
- Skip history。
- 无效 entry 跳过。
- 共同父路由保留。
- DisposeOnLeave。
- KeepAlive cache 命中。
- 插件停用清理 Journal 和缓存。
- Journal 不保存不可序列化对象。
