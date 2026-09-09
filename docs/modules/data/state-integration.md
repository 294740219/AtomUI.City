# AtomUI.City.Data State Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `State Integration` 相关实现决策，不重新定义模块边界。

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

- `DataConnectionOwnerKind` 只允许 Application、Window、Navigation、Route、Activation、Plugin 或 Manual。
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

## AtomUI.City.Data State Integration 设计

适用范围：Data 与 State 的显式更新、状态投影、ParentScope/cancellation、UI 线程和错误边界。

本页是跨模块集成合同，不代表 Data 直接依赖或自动写入 State。调用方必须在检查 `DataResult` 后显式使用 State writer；需要统一提交时可通过 ordered `IDataRequestHandler` 实现应用级 adapter。

### 1. 定位

Data 负责异步请求和缓存策略。State 负责应用状态表达。

Data 不隐式写全局 State。请求完成后是否写 State 必须由 ViewModel、service、resolver 或显式 adapter 决定。

### 2. 默认链路

```text
Command / Data request
-> DataRequestContext + optional ParentScope
-> DataResult<T>
-> explicit decision
-> State writer
-> State notification
-> Presentation update
```

### 3. 显式写入

允许：

- ViewModel 调用 State writer。
- Resolver 初始化 RouteScope state。
- Mutation success 后写 State。
- Subscription mapper 投影到 State。

禁止：

- Data client 自动把所有响应写入全局 State。
- transport callback 直接写 ViewModel。
- State update 中执行 IO 或再次发 Data 请求。

### 4. Late Result

ParentScope 取消或 operation 返回 `Cancelled`/`StaleSuppressed` 后不应继续提交状态更新。

规则：

- 提交 State 前检查 `DataResult.Status`；绑定 ParentScope 时 pipeline 已将 late result 映射为 `StaleSuppressed`。
- `LatestWins` 旧结果不能提交 State。
- Plugin contribution revoked 后不能提交 Host state。

### 5. Streaming 投影

Streaming / SignalR 消息可以显式投影到 State。

规则：

- SignalR mapper 随 `IDataSubscription` 撤销；standalone stream mapper 随 stream Dispose/ParentScope cancellation 结束。
- mapper 不能访问 UI。
- mapper 错误进入 diagnostics。
- backpressure drop 必须可诊断。

### 6. 错误策略

| 场景 | 默认处理 |
|---|---|
| State writer 拒绝 | DataResult 保留，State 写入失败进入诊断。 |
| Operation 已取消 | 抑制 State update。 |
| mapper 抛异常 | 记录错误，按 subscription policy。 |
| 插件未授权写 Host state | 拒绝写入。 |

### 7. 测试策略

对应跨模块集成能力落地时必须覆盖：

- DataResult 显式写入 State。
- cancelled operation 不写 State。
- LatestWins 旧结果不写 State。
- SignalR message 投影。
- plugin state 写入授权。
