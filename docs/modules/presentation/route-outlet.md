# AtomUI.City.Presentation Route Outlet 合同

## 适用范围

本专题属于 `AtomUI.City.Presentation` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Route Outlet` 相关实现决策，不重新定义模块边界。

## 设计决策

- Routing 只负责 Route -> ViewModel Target。
- 参数绑定失败必须返回导航失败结果。
- 插件路由撤销后 route graph 必须重新发布。
- Presentation 负责 ViewModel -> View -> Outlet -> VisualTree。
- VisualTree 变化必须通过生命周期事件或绑定反馈回 ViewModel/State。
- View 创建和提交必须在 UI dispatcher 上执行。

## Public Contract

- 只允许通过 `AtomUI.City.Presentation` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-PRESENTATION-001 | UI Dispatcher | AvaloniaUiDispatcherTests |
| AUC-PRESENTATION-002 | View Locator | ViewLocatorTests |
| AUC-PRESENTATION-003 | View Binding | ViewBindingTests |
| AUC-PRESENTATION-004 | Route Outlet | RouteOutletTests |
| AUC-PRESENTATION-005 | Visual Lifecycle Feedback | VisualFeedbackTests |
| AUC-PRESENTATION-006 | Interaction and Validation Bridge | PresentationInteractionHandlerTests; ValidationVisualStateBindingTests |
| AUC-PRESENTATION-007 | Localization and Resource Bridge | PresentationLocalizationBridgeTests; PresentationResourceRegistryTests |
| AUC-PRESENTATION-008 | Plugin UI Unload Coordination | ActivePluginViewRegistryTests; PresentationPluginUnloadCoordinatorTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.PluginSystem 运行时直接依赖插件实现类型` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Presentation Route Outlet 设计

适用范围：Route Outlet、commit plan、attach/detach/replace、提交失败回滚和诊断

### 1. 定位

Route Outlet 是 Routing 和 Presentation 的提交边界。

Routing 输出 Outlet commit plan，Presentation 执行 UI 提交。

```text
Commit plan
-> Find outlets
-> Create/bind new views
-> Attach or detach reused views
-> Update visual tree
-> Return commit result
```

### 2. IRouteOutlet

`IRouteOutlet` 应支持：

- Outlet name。
- 当前 content。
- Attach。
- Detach。
- Replace。
- Clear。
- Commit diagnostics。

默认 Outlet 名为 `primary`。

### 3. 规则

- Outlet 名称稳定，不能运行时动态变更。
- 命名 Outlet 不自动创建新的 NavigationScope。
- Commit 必须在 UI Thread。
- Commit 失败时必须尽量恢复旧 content。
- Presentation 不决定导航成功，只返回 commit result。
- 同一 Outlet 的 commit 串行执行；重复提交当前 handle 不释放当前 View。
- 取消发生在 attach 前时保持旧 content，并释放被拒绝的新 handle。
- 旧 handle dispose 失败时保持旧 content，新 handle 被释放，结果返回失败。
- `OutletCommitPlanned`、`OutletCommitSucceeded`、`OutletCommitFailed` 诊断必须包含 outlet、operation、view type 和 error 上下文。

### 4. 失败回滚

```text
Presentation commit failed
-> detach newly created view
-> dispose binding
-> dispose provisional ActivationScope
-> dispose provisional RouteScope
-> keep old outlet content
-> navigation failed with diagnostics
```

### 5. Routing 集成

Routing 提供：

- NavigationTransaction id。
- Outlet commit plan。
- ViewModel instance。
- RouteContext。
- Reuse / KeepAlive 指令。
- Contribution 信息。

Presentation 返回：

- Commit success。
- Commit failed。
- Failure stage。
- Created views。
- Attached / detached views。
- Disposal diagnostics。

### 6. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| primary outlet | Unit | 默认 Outlet 可提交内容。 |
| named outlet | Unit | 按名称找到目标 Outlet。 |
| replace | Unit | 旧 View detach，新 View attach。 |
| commit failure | Unit | 旧 content 保留。 |
| disposal diagnostics | Unit | detach/dispose 失败被聚合。 |
| repeated commit | Unit | 当前 handle 重复提交为 no-op。 |
| cancellation | Unit | attach 前取消不替换旧 content。 |
