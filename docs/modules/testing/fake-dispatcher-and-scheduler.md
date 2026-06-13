# AtomUI.City.Testing Fake Dispatcher And Scheduler 合同

## 适用范围

本专题属于 `AtomUI.City.Testing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Fake Dispatcher And Scheduler` 相关实现决策，不重新定义模块边界。

## 设计决策

- 默认不隐式切线程。
- 后台任务必须观察 cancellation。
- UI 更新必须进入 Presentation dispatcher。
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

## Fake Dispatcher 和确定性调度设计

适用范围：UI dispatcher fake、后台调度、Timer、延迟、异步回调和确定性测试

### 1. 目标

桌面应用测试不能依赖真实 UI thread、真实时钟和不可预测线程调度。Testing 必须提供可控调度环境。

### 2. FakeUiDispatcher

`FakeUiDispatcher` 模拟 UI dispatcher。

能力：

- 记录投递的 work item。
- 支持 `Drain` 执行队列。
- 支持断言是否投递到 UI target。
- 支持模拟 UI runtime 未准备。
- 支持取消排队 work item。
- 支持异常聚合。

规则：

- 默认不自动执行排队 work。
- 测试必须显式 drain。
- UI work 执行顺序必须稳定。
- Scope 停止后，对应 UI work 不应执行。

### 3. DeterministicScheduler

`DeterministicScheduler` 控制：

- background work。
- virtual time。
- Timer。
- debounce。
- throttle。
- retry delay。
- timeout。
- delayed cancellation。

测试通过推进虚拟时间触发行为。

```text
AdvanceBy(500ms)
-> run due timers
-> run scheduled callbacks
-> collect diagnostics
```

### 4. 禁止真实等待

测试中禁止使用真实 `Task.Delay` 猜测完成。

允许：

- 等待明确 completion task。
- drain scheduler。
- drain dispatcher。
- advance virtual time。
- 使用 CancellationToken 明确结束。

### 5. 线程目标断言

测试应能断言：

- UI 更新投递到 UI dispatcher。
- Data callback 未直接访问 UI。
- EventBus handler dispatch target 正确。
- State notification dispatch target 正确。
- Plugin unload 后无残留 dispatcher callback。

### 6. 错误处理

Fake dispatcher 和 scheduler 必须记录：

- work item id。
- owner scope。
- enqueue time。
- execution time。
- cancellation source。
- exception。

异常应进入 diagnostics collector，测试可以断言错误策略。

### 7. 测试要求

必须覆盖：

- UI work 排队和 drain。
- drain 顺序。
- work cancellation。
- virtual time 推进。
- Timer 触发。
- timeout。
- retry delay。
- Scope stop 后 work 不执行。
- 异常聚合。
