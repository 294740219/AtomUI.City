# AtomUI.City.Data Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 连接生命周期必须显式声明 owner。
- 请求取消后不得写入 State。
- HTTP、gRPC、SignalR 必须映射到统一 DataResult。
- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

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

## AtomUI.City.Data Diagnostics and Testing 设计

适用范围：Data 诊断字段、错误记录、测试替身、竞态测试、无 UI 测试和插件卸载测试。

### 1. 定位

Data 必须可诊断、可测试。

请求失败不能只表现为 ViewModel 没数据。必须能说明失败发生在哪个 client、哪个 operation、哪个 transport、哪个 retry attempt、哪个 scope 或哪个插件贡献。

### 2. 诊断字段

必须记录：

- OperationId。
- OperationScopeId。
- DataClientId。
- Operation name。
- Transport kind。
- Request correlation id。
- RouteId。
- ActivationScopeId。
- PluginId。
- ContributionId。
- Auth result。
- Cache hit/miss。
- Retry attempt。
- Timeout / deadline。
- Backpressure action。
- Connection state。
- Transport status。
- DataError kind。
- Dispatch target。
- Duration。

敏感信息不能写入日志：token、password、完整 credential、完整 authorization header。

### 3. 测试替身

Testing 包应提供：

- Fake data client。
- Fake HTTP transport。
- Fake gRPC transport。
- Fake SignalR transport。
- Test request pipeline。
- Fake access token provider。
- Fake cache。
- Fake resilience policy。
- Data diagnostics recorder。
- Test connection manager。
- Test stream producer。
- Plugin data client test host。
- Deterministic scheduler。

### 4. 竞态测试

必须覆盖：

- 请求完成时 Scope 已取消，结果不提交。
- `CancelPrevious` 旧请求返回晚于新请求。
- `LatestWins` 只提交最新结果。
- SignalR handler 在后台线程回调。
- gRPC stream 慢消费者触发 backpressure。
- token refresh 并发合并。
- 插件卸载时仍有请求、连接、订阅。
- cache 按 principal 隔离。
- mutation retry 被禁止。
- UI dispatcher 不存在时 Data 仍可测试。

### 5. Transport 测试

必须覆盖：

- HTTP 200 / 401 / 403 / 404 / 409 / 5xx。
- gRPC status mapping。
- gRPC deadline exceeded。
- gRPC server streaming。
- SignalR connect / reconnect / closed。
- SignalR server push。
- upload / download progress。

### 6. 无 UI 测试

Data 测试不得依赖真实 AtomUI/Avalonia UI。

规则：

- 不要求 UI dispatcher 存在。
- 使用 deterministic scheduler。
- 手动推进 stream 和 connection state。
- 明确断言 dispatch target。

### 7. 插件卸载测试

必须覆盖：

- 插件请求取消。
- 插件 stream 取消。
- 插件 SignalR connection stop。
- 插件 callbacks 清理。
- 插件 cache revoke。
- 无插件私有类型引用残留。
