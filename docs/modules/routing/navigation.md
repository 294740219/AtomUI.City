# AtomUI.City.Routing Navigation 合同

## 适用范围

本专题属于 `AtomUI.City.Routing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Navigation` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Routing Navigation 设计

适用范围：IRouter、NavigationScope、NavigationTarget、NavigationTransaction、NavigationResult、并发策略、提交和回滚。

### 1. 定位

Navigation 是 Routing 的运行时核心。它把开发者的导航请求转换为事务式路由切换，并保证当前页面在候选页面准备好之前不被破坏。

### 2. IRouter

`IRouter` 是某个 NavigationScope 内的导航入口。

建议接口：

```csharp
public interface IRouter
{
    ValueTask<NavigationResult> NavigateAsync(
        RouteReference route,
        NavigationOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<NavigationResult> NavigateAsync<TParameters>(
        RouteReference<TParameters> route,
        TParameters parameters,
        NavigationOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<NavigationResult> NavigateByPathAsync(
        string path,
        NavigationOptions? options = null,
        CancellationToken cancellationToken = default);

    ValueTask<NavigationResult> BackAsync(
        CancellationToken cancellationToken = default);

    ValueTask<NavigationResult> ForwardAsync(
        CancellationToken cancellationToken = default);
}
```

`NavigateByPathAsync` 只作为 Deep Link、命令行入口、外部 URI、测试和兼容入口，不作为日常代码主路径。

### 3. NavigationTarget

所有请求先规范化为 `NavigationTarget`。

来源：

- RouteReference。
- RouteReference + parameters。
- Path。
- Deep Link URI。
- Journal entry。
- Redirect result。

`NavigationTarget` 应包含：

- Target kind。
- RouteId 或 path。
- 强类型参数。
- Query 参数。
- Fragment。
- NavigationOptions。
- 来源诊断信息。

### 4. NavigationOptions

建议选项：

| 选项 | 说明 |
|---|---|
| `Mode` | Push、Replace、Reset。 |
| `HistoryBehavior` | Record、Skip、ReplaceCurrent。 |
| `ConcurrencyPolicy` | CancelPrevious、Queue、RejectIfBusy。 |
| `RestoreState` | 是否从 Journal 恢复 route state。 |
| `ForceReload` | 是否忽略可复用分支。 |
| `AllowRedirect` | 是否允许 Guard / Resolver 重定向。 |
| `Timeout` | 导航超时。 |

默认模式为 Push，默认并发策略为同 Scope 内 Commit 前取消旧导航，Commit 中排队。

### 5. NavigationScope

NavigationScope 生命周期通常挂在 WindowScope 下。

它负责：

- 持有 Router。
- 持有当前 NavigationSnapshot。
- 持有 Journal。
- 串行化导航。
- 跟踪活动 RouteScope。
- 接入 UI Dispatcher。
- 接入诊断。

NavigationScope 停止时：

```text
Reject new navigation
-> Cancel running transaction
-> Dispose active route tree
-> Clear journal
-> Mark stopped
```

### 6. NavigationTransaction

一次导航创建一个 transaction。

阶段：

```text
Created
-> Matching
-> Planning
-> Guarding
-> ConfirmingLeave
-> Resolving
-> CreatingViewModels
-> PreparingCommit
-> Committing
-> Completed
```

失败终态：

```text
Rejected
Cancelled
Failed
RolledBack
```

Transaction 必须记录阶段耗时和阶段结果。

### 7. 导航计划

NavigationPlan 由当前路由树和目标路由树 diff 得出。

应包含：

- 保留的 route branch。
- 离开的 route branch。
- 新增的 route branch。
- 参数变化但 route id 相同的 branch。
- 需要重新解析数据的 branch。
- 需要重新激活 ViewModel 的 branch。
- Outlet commit plan。

共同父路由默认保留，减少不必要的 ViewModel 重建。

### 8. Provisional RouteScope

新增路由分支先创建 provisional RouteScope。

规则：

- Provisional RouteScope 不进入当前 NavigationSnapshot。
- Provisional RouteScope 可以创建服务作用域。
- Provisional RouteScope 可以运行 Resolver。
- Provisional RouteScope 可以创建候选 ViewModel。
- Provisional RouteScope 可以创建候选 ActivationScope，用于注册 Presentation binding、UI 事件订阅和 Interaction handler 的释放边界。
- 候选 ActivationScope 只有在 Presentation commit 成功后才进入 running / active 状态。
- Commit 失败或准备失败时必须释放。

这样可以保证当前页面在候选页面准备完成前仍保持活动。

### 9. Commit

Commit 必须在 UI Thread 上执行。

Commit 步骤：

```text
Stop accepting current route operations that will leave
-> Apply Presentation outlet changes
-> Switch current NavigationSnapshot atomically
-> Mark candidate RouteScopes active
-> Mark candidate ActivationScopes running
-> Activate added ViewModels
-> Deactivate removed ViewModels
-> Update Journal
```

如果 Presentation commit 失败：

- 恢复原 Outlet 状态。
- 释放候选 RouteScope。
- 保持原 NavigationSnapshot。
- 返回 Failed。

Commit 开始后不允许被新导航打断。

### 10. 回滚

准备阶段回滚：

```text
Cancel provisional scopes
-> Dispose provisional ViewModels
-> Dispose provisional service scopes
-> Keep current snapshot unchanged
```

Commit 阶段失败回滚：

```text
Try restore old outlet content
-> Reactivate old branch if needed
-> Dispose candidate branch
-> Report commit failure
```

回滚失败必须进入 ErrorPolicy，并保留最大诊断信息。

### 11. 并发策略

同一个 NavigationScope 内默认串行。

策略：

| 策略 | 行为 |
|---|---|
| `CancelPrevious` | 新请求取消 Commit 前旧请求。 |
| `Queue` | 新请求排队。 |
| `RejectIfBusy` | 正在导航时直接拒绝。 |

Commit 中始终排队或拒绝，不取消。

不同 NavigationScope 可以并行导航，但如果共享插件卸载、全局配置切换等外部操作，需要通过对应模块的生命周期锁协调。

### 12. Redirect

Guard 或 Resolver 可以返回 redirect。

规则：

- Redirect 生成新的 NavigationTarget。
- Redirect 继承原导航的诊断链。
- Redirect 计数必须有限制。
- 静态 redirect 循环由 Source Generator 诊断。
- 动态 redirect 循环由运行时检测。

### 13. NavigationResult

建议结果：

| 结果 | 说明 |
|---|---|
| `Success` | 导航完成。 |
| `Rejected` | Guard 或策略拒绝。 |
| `Redirected` | 已重定向并完成或交给后续目标。 |
| `Cancelled` | 被取消。 |
| `Failed` | 异常失败。 |
| `NotFound` | 无匹配路由。 |
| `StaleRouteGraph` | 使用的路由图已不可用。 |
| `ContributionRevoked` | 目标贡献已撤销。 |

结果必须包含 NavigationId、目标、失败阶段和诊断信息。

### 14. 状态暴露

NavigationScope 应暴露当前状态：

```text
IStateValue<NavigationSnapshot>
IStateValue<NavigationStatus>
```

外部观察当前路由状态使用 State。EventBus 只发布导航完成、失败、取消等事实事件，不作为控制流。

### 15. 测试要求

测试必须覆盖：

- RouteReference 导航。
- Path 导航。
- Replace / Reset。
- Back / Forward。
- Guard 拒绝后当前页面不变。
- Resolver 失败后释放候选 scope。
- Commit 失败回滚。
- Commit 中新导航排队。
- 不同 NavigationScope 并行。
- Redirect 循环检测。
