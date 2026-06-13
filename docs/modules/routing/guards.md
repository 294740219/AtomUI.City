# AtomUI.City.Routing Guards 合同

## 适用范围

本专题属于 `AtomUI.City.Routing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Guards` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Routing Guards 设计

适用范围：Match Policy、Enter Guard、Leave Guard、离开确认、重定向、权限集成、线程和诊断。

### 1. 定位

Guard 是导航决策点。它决定某个路由是否可以匹配、是否可以进入、当前路由是否可以离开。

Guard 不负责数据加载，不负责 UI 渲染，不保存业务状态。

### 2. Guard 类型

第一版建议三类：

| 类型 | 执行时机 | 用途 |
|---|---|---|
| `IRouteMatchPolicy` | 路由匹配阶段 | 功能开关、插件状态、环境条件。 |
| `IRouteEnterGuard` | 进入候选路由前 | 登录、权限、运行条件。 |
| `IRouteLeaveGuard` | 离开当前路由前 | 未保存修改、任务中断确认。 |

Match Policy 返回 false 时，该路由不参与匹配。Enter/Leave Guard 返回拒绝时，本次导航失败或重定向。

### 3. 结果模型

Guard 不返回裸 bool。

建议结果：

```text
Allow
Reject(reason)
Redirect(target)
Cancel
Failed(error)
```

Reject 是业务或策略拒绝。Failed 是 Guard 自身异常或不可恢复错误。Cancel 表示用户取消或外部取消。

### 4. 执行顺序

进入目标路由：

```text
Match root policy
-> Match child policy
-> Enter root guard
-> Enter child guard
```

离开当前路由：

```text
Leave leaf guard
-> Leave parent guard
```

这样更符合桌面页面退出语义：最内层页面先确认是否能离开。

### 5. Leave Guard 与 MVVM

Mvvm 提供 ViewModel 侧能力，例如：

```text
ICanDeactivate
IConfirmDeactivate
```

Routing 在 Leave Guard 阶段调用这些能力。

规则：

- Mvvm 只定义 ViewModel 能力和结果。
- Routing 决定是否继续导航。
- Presentation 只承接需要 UI 的 Interaction。
- 用户取消返回 Cancel，不是异常。
- 插件强制停用时可以进入强制离开策略。

### 6. 权限集成

Security 模块可以提供 Guard。

Routing 只传入：

- RouteId。
- Route metadata。
- 当前 principal。
- 参数。
- Contribution 来源。

Routing 不存储权限，也不解释业务权限语义。

### 7. 重定向

Guard 可以返回 redirect。

规则：

- Guard 内部不直接调用 `IRouter`。
- Redirect 由 NavigationTransaction 统一处理。
- Redirect 目标重新进入完整导航流程。
- Redirect 必须带来源诊断。
- Redirect 次数有限制。

典型场景：

- 未登录跳转登录路由。
- 无权限跳转拒绝访问路由。
- 插件路由失效跳转安全 fallback。

### 8. 线程和取消

Guard 必须接收 CancellationToken。

规则：

- Guard 不访问 UI 对象。
- 需要用户确认时通过 MVVM Interaction 进入 Presentation。
- 长耗时 Guard 应通过 OperationScope 或受管后台任务执行。
- 插件停用会取消插件 Guard。
- 导航取消时 Guard 应尽快返回 Cancel。

### 9. AOT 和注册

Guard 通过 Route Map 显式声明。

```csharp
[RouteGuards(typeof(ProfileAccessGuard))]
```

Source Generator 必须校验：

- 类型实现对应 contract。
- 类型可由 DI 创建。
- 插件路由引用类型不越界。
- Guard 顺序稳定。

运行时不扫描程序集找 Guard。

### 10. 错误策略

| 场景 | 默认处理 |
|---|---|
| Match Policy false | 尝试其他候选路由。 |
| Enter Guard Reject | Navigation rejected。 |
| Leave Guard Reject | 保持当前页面。 |
| Guard Cancel | Navigation cancelled。 |
| Guard 抛异常 | Navigation failed。 |
| Redirect 循环 | Navigation failed with diagnostics。 |

### 11. 诊断

必须记录：

- Guard 类型。
- 所属 RouteId。
- 所属 Contribution。
- 执行顺序。
- 耗时。
- 结果。
- Redirect 目标。
- 错误信息。

### 12. 测试要求

测试必须覆盖：

- Match Policy 排除候选路由。
- Enter Guard 拒绝。
- Leave Guard 拒绝。
- 用户取消离开。
- Guard redirect。
- Redirect 循环。
- Guard 异常。
- 插件停用取消 Guard。
