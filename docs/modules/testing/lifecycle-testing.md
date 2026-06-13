# AtomUI.City.Testing Lifecycle Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Testing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Lifecycle Testing` 相关实现决策，不重新定义模块边界。

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

## Lifecycle 测试设计

适用范围：Lifecycle Scope、Lifecycle pipeline、middleware、Operation、Lease、取消、释放和错误聚合

### 1. 目标

生命周期是 AtomUI.City 的运行骨架。Testing 必须能确定性验证 Scope、Operation、Lease 和 middleware 的顺序与释放。

### 2. LifecycleDriver

`LifecycleDriver` 负责驱动：

- Host start。
- Application start。
- Scope create。
- Scope stop。
- Operation run。
- Lease create/revoke。
- Middleware execution。
- Dispose。

### 3. Scope 断言

必须支持断言：

- Scope 创建顺序。
- Scope 父子关系。
- Scope 状态。
- Scope stop 顺序。
- Scope dispose 顺序。
- CancellationToken 是否触发。
- Stop 幂等。

### 4. Middleware 断言

必须支持：

- 执行顺序断言。
- 短路断言。
- 异常策略断言。
- cancellation 传递断言。
- diagnostics 断言。

### 5. Operation 测试

Operation 测试必须覆盖：

- 成功完成。
- 失败完成。
- 取消。
- owner scope stop 后自动取消。
- late result suppression。
- 诊断记录。

### 6. Lease 测试

Lease 测试必须覆盖：

- 创建。
- revoke。
- 反向撤销顺序。
- revoke 幂等。
- revoke 失败聚合。
- owner scope stop 后自动撤销。

### 7. 错误聚合

释放阶段不能因为单个错误阻断其他释放动作。

测试必须断言：

- 所有释放动作都被尝试。
- 错误被聚合。
- 错误上下文包含 phase 和 scope id。
- cancellation 不被当成普通失败。

### 8. 测试要求

必须覆盖：

- Scope tree。
- Middleware 顺序。
- Operation cancellation。
- Lease revoke。
- Stop 幂等。
- Dispose 错误聚合。
- Plugin unload 生命周期路径。
