# AtomUI.City.Testing Integration Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Testing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Integration Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

## Public Contract

- 只允许通过 `AtomUI.City.Testing` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-TESTING-001 | Test Host | TestHostTests |
| AUC-TESTING-002 | Fake Dispatcher | FakeUiDispatcherTests |
| AUC-TESTING-003 | Deterministic Scheduler | SharedTestUtilitiesTests |
| AUC-TESTING-004 | Module Test Host | ModuleTestHostTests |
| AUC-TESTING-005 | Plugin Test Host | PluginTestHostTests |
| AUC-TESTING-006 | Routing Test Host | RoutingTestHostTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `生产运行时反向依赖 AtomUI.City.Testing` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## 集成测试策略

适用范围：框架内集成测试、平台集成测试、模板 smoke 测试和 CI 分类

### 1. 目标

集成测试用于验证模块协作和真实运行时风险。它不替代单元测试。

核心规则：

- 单元测试覆盖每个功能点。
- 集成测试覆盖跨模块链路。
- 平台集成测试覆盖 fake runtime 无法证明的真实 UI 行为。

### 2. 框架内集成测试

框架内集成测试使用 `FrameworkIntegrationTestHost`，默认不启动真实 AtomUI/Avalonia UI。

覆盖链路：

| 场景 | 覆盖链路 |
|---|---|
| Host 启动 | Configuration -> DI -> ModuleGraph -> Lifecycle |
| 路由进入页面 | Routing -> Security Guard -> Resolver -> ViewModel Target -> Fake Presentation Outlet |
| 页面激活 | Routing -> MVVM Activation -> Fake Presentation Commit -> Lifecycle Scope |
| 状态变更 | State -> Subscription -> Dispatcher -> ViewModel reaction |
| 事件通知 | EventBus -> Handler -> Lifecycle cancellation -> Diagnostics |
| 数据请求 | Data -> Security token -> Request pipeline -> Fake HTTP/gRPC/SignalR |
| 多语言切换 | Localization -> Culture state -> Fake Presentation resource refresh |
| 插件启用 | PluginSystem -> Module -> Contribution -> Routing/Presentation/EventBus/Data |
| 插件卸载 | Stop entry -> Cancel operations -> Revoke leases -> Unload assertions |

这些测试应稳定、快速、可并行，CI 每次执行。

### 3. 平台集成测试

平台集成测试使用真实 AtomUI/Avalonia runtime。

只覆盖 fake runtime 无法证明的行为：

- UI Dispatcher 回到真实 UI thread。
- ViewLocator 找到真实 View。
- ViewModel 到 View binding。
- Route Outlet 提交到 visual tree。
- ResourceDictionary 和主题刷新。
- Window lifecycle。
- visual attach/detach feedback。
- 插件 View/Resource 卸载后无残留 UI 引用。

平台集成测试应独立分类：

```text
Category=PlatformIntegration
Category=RequiresUiRuntime
```

普通 PR 可以只跑框架内集成测试，夜间或 release pipeline 跑平台集成测试。

### 4. 模板 Smoke 测试

模板 smoke 测试证明工程化入口可用。

覆盖：

- 创建应用模板。
- 构建应用。
- 生成模块。
- 生成页面。
- 生成插件。
- 插件打包。
- 插件安装到测试 Host。
- 执行一次导航。
- 执行一次插件卸载。

Smoke 测试不追求覆盖所有边界，边界由单元测试和集成测试覆盖。

### 5. 项目建议

建议测试项目：

```text
tests/
  AtomUI.City.FrameworkIntegrationTests/
  AtomUI.City.PlatformIntegrationTests/
  AtomUI.City.TemplateSmokeTests/
```

现有模块测试项目继续承载单元测试和模块内 contract test。

### 6. 失败回补规则

集成测试发现 bug 后：

- 如果 bug 可在单模块复现，必须补单元测试。
- 如果 bug 是 contract 不一致，必须补 contract test。
- 如果 bug 只存在真实 UI runtime，补平台集成测试。
- 如果 bug 涉及构建或模板，补 build test 或 smoke test。

集成测试不能成为缺少单元测试的理由。
