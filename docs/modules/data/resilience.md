# AtomUI.City.Data Resilience 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Resilience` 相关实现决策，不重新定义模块边界。

## 设计决策

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

## AtomUI.City.Data Resilience 设计

适用范围：timeout、retry、circuit breaker、fallback、rate limit、mutation 重试约束和重试诊断。

### 1. 定位

Resilience 负责提高 Data operation 对网络抖动、服务暂时不可用和超时的容错能力。

Data 可以使用 Polly 作为策略实现，但 Data API 不应把 Polly 类型扩散到 ViewModel、Routing 或 State。

### 2. 策略类型

第一版支持：

- Timeout。
- Retry。
- Circuit breaker。
- Fallback。
- Rate limit。

策略由 operation descriptor、client metadata 或 Host 配置提供。

### 3. Timeout / Deadline

规则：

- HTTP timeout 映射为 Data timeout。
- gRPC timeout 映射为 deadline。
- SignalR hub invoke timeout 映射为 operation timeout。
- Streaming 和 SignalR connection 不能只有总 timeout，还需要 idle timeout 或 keepalive 诊断。

### 4. Retry

默认规则：

- Query 可以按策略 retry。
- Mutation 默认不自动 retry。
- 取消不 retry。
- Transport exception 必须先映射为 `TransportError`，再按 retry policy 判断是否重试。
- 403 不 retry。
- 401 refresh 成功后最多按策略重试一次。
- streaming item handler 不按普通 request retry。

Mutation 只有声明幂等或提供 idempotency key 时才允许自动 retry。

### 5. Circuit Breaker

Circuit breaker 绑定 client 或 operation。

规则：

- breaker 状态进入 diagnostics。
- breaker open 时返回 PolicyRejected 或 ServiceUnavailable。
- 插件 client 的 breaker 随插件 contribution 撤销。

### 6. Fallback

Fallback 必须显式声明。

允许：

- 返回本地缓存。
- 返回降级数据。
- 返回默认空结果。

禁止：

- 隐式吞掉认证失败。
- 隐式吞掉权限不足。
- 隐式写 State。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| timeout | Timeout 或 DeadlineExceeded。 |
| retry exhausted | 保留最后错误并记录 attempts。 |
| circuit open | PolicyRejected / ServiceUnavailable。 |
| fallback failed | 返回原始错误和 fallback 诊断。 |

### 8. 测试策略

测试必须覆盖：

- query retry。
- mutation 默认不 retry。
- idempotent mutation retry。
- timeout。
- circuit open。
- fallback cache。
- retry attempts diagnostics。
