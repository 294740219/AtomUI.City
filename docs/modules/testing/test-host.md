# AtomUI.City.Testing Test Host 合同

## 适用范围

本专题属于 `AtomUI.City.Testing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Test Host` 相关实现决策，不重新定义模块边界。

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

## TestHost 设计

适用范围：测试 Host、测试服务容器、模块组合、fake runtime 注入和 Host 生命周期驱动

### 1. 目标

`TestHost` 用于在测试中构造 AtomUI.City 运行时骨架。它应尽量接近真实 Host，但允许替换 UI、调度、数据传输、插件源和诊断输出。

### 2. 职责

`TestHost` 负责：

- 创建测试 ServiceProvider。
- 创建 `ApplicationScope`。
- 注入测试 Configuration。
- 注入测试 Module。
- 注入 fake dispatcher 和 deterministic scheduler。
- 创建 Lifecycle pipeline。
- 创建 Contribution registry。
- 收集 diagnostics。
- 按测试要求启用模块测试适配器。

`TestHost` 不负责：

- 启动真实 UI runtime。
- 访问真实用户插件目录。
- 访问真实远程服务。
- 绑定具体测试 runner。

### 3. Builder 模型

推荐 API 风格：

```csharp
var host = TestHost.CreateBuilder()
    .UseProperty("scenario", "module-start")
    .UseConfiguration(values => { })
    .UseModule<TestModule>()
    .UseRoutingTesting()
    .UseFakePresentation()
    .UsePluginTesting()
    .Build();
```

`UseProperty` 只构造 `TestHost.Properties` 的只读测试数据快照，不向生产 `IApplicationContext` 注入动态字段。测试 Context 与生产合同一致，只包含不可变应用实例描述字段。

命名可以在实现阶段调整，但设计要求保持 builder 风格，符合 .NET 扩展方法习惯。

### 4. 默认服务

TestHost 默认包含：

- Core DI。
- Configuration。
- Options。
- Diagnostics collector。
- Lifecycle kernel。
- Fake UI dispatcher。
- Deterministic scheduler。
- Contribution test registry。

模块能力通过显式扩展方法启用。

### 5. 生命周期

TestHost 生命周期：

```text
Create builder
-> configure services
-> build service provider
-> create ApplicationScope
-> start lifecycle
-> run test
-> stop lifecycle
-> dispose scopes
-> assert no leaks
```

规则：

- `StopAsync` 必须幂等。
- `DisposeAsync` 必须释放所有 Scope。
- 默认在释放时断言没有未完成 Operation。
- 默认在释放时断言没有未撤销 Lease。

### 6. 模块组合

TestHost 支持：

- 单模块测试。
- 多模块组合测试。
- 插件模块测试。
- Host contract 测试。

模块图顺序应可断言。模块初始化失败应可注入并断言错误策略。

### 7. 测试数据隔离

每个 TestHost 必须有独立测试目录：

```text
<Temp>/AtomUI.City.Tests/<test-run-id>/
```

目录包含：

- configuration。
- plugin cache。
- plugin installed。
- generated manifests。
- diagnostics。

测试结束默认清理。失败时可按策略保留目录用于诊断。

### 8. 断言

TestHost 应提供：

- 生命周期状态断言。
- Scope 树断言。
- Diagnostics 断言。
- 未完成任务断言。
- 未释放资源断言。
- Contribution Lease 断言。

### 9. 测试要求

TestHost 自身必须测试：

- 默认 Host 创建。
- 配置注入。
- 模块注入。
- 生命周期 start/stop。
- stop 幂等。
- dispose 聚合错误。
- 测试目录隔离。
- fake runtime 替换真实 runtime。
