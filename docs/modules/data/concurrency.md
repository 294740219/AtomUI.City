# AtomUI.City.Data Concurrency 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Concurrency` 相关实现决策，不重新定义模块边界。

## 设计决策

- 默认不隐式切线程。
- 后台任务必须观察 cancellation。
- UI 更新必须进入 Presentation dispatcher。
- 连接生命周期必须显式声明 owner。
- 请求取消后不得写入 State。
- HTTP、gRPC、SignalR 必须映射到统一 DataResult。

## Public Contract

- 只允许通过 `AtomUI.City.Data` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-DATA-001 | Request Pipeline | DataPipelineTests |
| AUC-DATA-002 | HTTP Transport | HttpDataTransportTests |
| AUC-DATA-003 | gRPC Transport | GrpcDataTransportTests |
| AUC-DATA-004 | SignalR Transport | SignalRDataTransportTests |
| AUC-DATA-005 | Connection Lifecycle | DataConnectionLifecycleTests |
| AUC-DATA-006 | Authentication | AccessTokenCredentialProviderTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data Concurrency 设计

适用范围：Data operation 并发策略、请求去重、排队、取消旧请求、最新结果提交和 keyed serial。

### 1. 定位

Data operation 必须明确并发行为。

桌面应用中搜索、保存、自动刷新、导航预取、SignalR 重连和插件请求经常并发发生。如果没有统一策略，就会出现旧结果覆盖新结果、重复保存、重复刷新和不可诊断的竞态。

### 2. 并发策略

| 策略 | 说明 |
|---|---|
| `AllowConcurrent` | 默认允许并发。 |
| `DisallowConcurrent` | 正在执行时拒绝新请求。 |
| `Queue` | 排队顺序执行。 |
| `CancelPrevious` | 新请求取消旧请求。 |
| `LatestWins` | 允许并发，但只有最新结果可提交。 |
| `KeyedSerial` | 同一个 resource key 串行，不同 key 并行。 |

策略由 operation descriptor、client metadata 或调用方显式指定。

### 3. LatestWins

`LatestWins` 适合搜索、筛选、自动补全。

规则：

- 允许多个请求并发。
- 每次请求分配 monotonic sequence。
- 只有最新 sequence 的结果允许提交。
- 旧请求完成后结果被抑制。
- 旧请求可以选择不取消，但不能写状态。

### 4. CancelPrevious

`CancelPrevious` 适合最新请求完全替代旧请求的场景。

规则：

- 新请求到来时取消旧 OperationScope。
- 旧请求返回 Cancelled。
- 如果 transport 无法立即取消，返回后也必须 suppress result。

### 5. Queue

`Queue` 适合必须顺序执行的 mutation。

规则：

- 同一 operation key 下顺序执行。
- 队列绑定 owner scope。
- owner scope 停止时取消未执行项。
- 队列长度应有限制或可诊断。

### 6. KeyedSerial

`KeyedSerial` 适合按资源串行，例如同一 document 的保存。

规则：

- key 相同串行。
- key 不同可以并行。
- key 必须可诊断。
- key 不能包含敏感信息。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| DisallowConcurrent 冲突 | 返回 PolicyRejected。 |
| Queue owner 停止 | 取消队列项。 |
| LatestWins 旧结果返回 | suppress result。 |
| KeyedSerial key 无效 | DataResult failed。 |

### 8. 测试策略

测试必须覆盖：

- 并发请求允许。
- DisallowConcurrent 拒绝。
- Queue 顺序。
- CancelPrevious 取消旧请求。
- LatestWins 只提交最新结果。
- KeyedSerial 同 key 串行、不同 key 并行。
