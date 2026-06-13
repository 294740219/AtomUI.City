# AtomUI.City.Testing Diagnostics And Assertions 合同

## 适用范围

本专题属于 `AtomUI.City.Testing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Assertions` 相关实现决策，不重新定义模块边界。

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

## 诊断和断言设计

适用范围：诊断收集、错误码断言、生命周期断言、线程断言、Contribution 断言和泄漏断言

### 1. 目标

Testing 必须提供统一断言工具，让测试能够断言行为、诊断、错误策略和资源释放。

### 2. DiagnosticsCollector

测试诊断收集器记录：

- event id。
- phase。
- scope id。
- operation id。
- module id。
- plugin id。
- contribution id。
- error code。
- exception。
- policy result。

### 3. 断言类型

| 断言 | 用途 |
|---|---|
| `LifecycleAssertions` | Scope、Operation、middleware、dispose。 |
| `ContributionAssertions` | Contribution、Lease、冲突、撤销。 |
| `ThreadingAssertions` | Dispatcher target、scheduler、未完成任务。 |
| `DiagnosticsAssertions` | 诊断事件、错误码、上下文。 |
| `PluginUnloadAssertions` | 插件卸载、ALC、残留引用。 |
| `StateAssertions` | 状态版本、通知、snapshot。 |
| `EventBusAssertions` | 发布、订阅、顺序、错误。 |
| `RoutingAssertions` | 匹配、导航事务、回滚。 |

### 4. 错误码断言

承诺错误码的功能必须在测试中断言错误码。

断言内容：

- error code。
- phase。
- source。
- context ids。
- policy result。

只断言异常类型不够。

### 5. 泄漏断言

涉及生命周期和插件的测试必须断言：

- 无 active operation。
- 无 active lease。
- 无 active subscription。
- 无 dispatcher callback。
- 无 timer。
- 无 Data connection。
- 插件加载上下文可释放，如果适用。

### 6. 快照和记录

Testing 可以提供结构化 snapshot，用于断言复杂状态。

规则：

- Snapshot 必须稳定。
- Snapshot 不包含不可预测时间戳。
- Snapshot 不包含绝对临时路径，除非测试明确断言路径。
- Snapshot 不包含敏感信息。

### 7. 测试要求

断言工具自身必须有单元测试，覆盖成功断言、失败断言和错误消息质量。
