# AtomUI.City.Data Request Pipeline 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Request Pipeline` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Data Request Pipeline 设计

适用范围：请求上下文、管线阶段、handler、OperationScope、认证注入、缓存、resilience、响应映射和诊断。

### 1. 定位

Request pipeline 是 Data 的核心执行链路。

所有 request/response 类型访问必须进入 pipeline。Streaming 和 realtime connection 也应复用 pipeline 中的认证、诊断、capability、resilience 和错误映射阶段。

### 2. Pipeline 阶段

推荐阶段：

```text
Create request context
-> Attach OperationScope
-> Validate request metadata
-> Check plugin capability
-> Resolve authentication credential
-> Build transport request
-> Cache lookup
-> Execute resilience policy
-> Send transport request
-> Map transport response
-> Map error
-> Cache write
-> Return DataResult
-> Emit diagnostics
```

第一版用固定阶段，避免复杂动态排序。

### 3. Request Context

Request context 应包含：

- OperationId。
- DataClientId。
- Operation name。
- Parent scope。
- CancellationToken。
- Transport kind。
- Auth metadata。
- Cache metadata。
- Resilience metadata。
- Plugin contribution。
- Correlation id。
- Diagnostics context。

Request context 不能包含 UI 控件实例。

### 4. OperationScope

每次请求都是 Operation。

规则：

- 调用方没有提供 OperationScope 时，Data 创建一个。
- 请求取消应联动 parent scope cancellation token。
- OperationScope 停止后禁止提交结果。
- Operation 完成、失败、取消都要记录诊断。

### 5. Handler

`IDataRequestHandler` 用于实现管线阶段。

规则：

- Handler 必须是可组合、可测试的。
- Handler 不访问 UI。
- Handler 必须尊重 cancellation token。
- Handler 异常进入 DataError mapping。
- 插件 handler 必须绑定插件 contribution。

### 6. 结果提交

结果提交前必须检查：

- OperationScope 是否仍 running。
- Parent ActivationScope / RouteScope 是否仍有效。
- Plugin contribution 是否仍 active。
- 当前 operation 是否被并发策略允许提交。

如果检查失败，结果被抑制，返回 cancelled 或 stale result 诊断。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| metadata 无效 | DataResult failed。 |
| capability 拒绝 | PolicyRejected 或 PluginUnavailable。 |
| credential 不可用 | AuthenticationRequired 或 CredentialUnavailable。 |
| transport 抛异常 | 映射为 DataError。 |
| result stale | 抑制提交，记录诊断。 |

### 8. 测试策略

测试必须覆盖：

- handler 顺序。
- credential 注入。
- cache short-circuit。
- retry 包裹 transport。
- transport error mapping。
- stale result suppression。
- OperationScope 取消。
