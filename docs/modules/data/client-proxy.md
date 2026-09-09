# AtomUI.City.Data Client Proxy 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Client Proxy` 相关实现决策，不重新定义模块边界。

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
| AUC-DATA-016 | Client Descriptors and Generation | AtomUICityIncrementalGeneratorDataTests; Data AOT fixture |
| AUC-DATA-017 | Plugin Data Contributions | DataPluginLifecycleTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data Client Proxy 设计

适用范围：typed client、generated client、adapter client、client descriptor、Refit 可选适配和 AOT/source generator。

### 1. 定位

Client proxy 负责给应用提供可注入、可测试、可诊断的数据客户端入口。

Data Core 不强制应用使用某一种代理框架。Data 1.0 目标支持 typed client 和 generated client；Refit、RPC、本地服务等通过 adapter 包接入。

1.0 已完成 `IDataClient`、`IDataClientFactory`、运行时 `DataClientRegistry`、generated descriptor catalog 和插件可撤销贡献。可选 adapter package 不属于 Data Core 1.0。
`DataClientRegistry` 按 contract type 注册且线程安全；同一 type 后注册者替换前注册者，`Unregister<TClient>` 只移除该 type 当前值。它不是插件贡献所有权或安全边界。

### 2. Client 类型

| 类型 | 说明 |
|---|---|
| Typed client | 推荐默认方式，符合 .NET DI 和测试习惯。 |
| Generated descriptor | Source Generator 生成 descriptor registrar；业务 client 实现继续由应用 typed client 或可选 adapter 提供。 |
| Adapter client | Refit、RPC、本地服务、插件服务等适配。 |

Data client 不应直接暴露裸 transport 给 ViewModel。

### 3. Client Descriptor

Client descriptor 应包含：

- ClientId。
- Transport kind。
- Operation descriptors。
- Auth metadata。
- Cache metadata。
- Resilience metadata。
- Serializer metadata。
- Connection lifetime metadata。
- Plugin contribution。

Descriptor 由显式注册或 Source Generator 生成。

### 4. Operation Descriptor

Operation descriptor 应包含：

- Operation name。
- Request/response 类型。
- Access mode：query、mutation、subscription、upload、download。
- Concurrency policy。
- Timeout / deadline。
- Retry policy。
- Cache policy。
- Auth policy。
- Diagnostics category。

### 5. Refit 适配

Refit 可以作为 `AtomUI.City.Data.Refit` 可选适配包。

规则：

- Refit 不进入 Data Core 唯一范式。
- Refit client 也必须进入 Data pipeline。
- Refit metadata 必须转换为 Data descriptor。
- 错误必须映射为 DataError。

### 6. 插件 client

插件 client 必须通过 Contribution 注册。

规则：

- 插件 client descriptor 必须可撤销。
- 插件停用后不能创建新 client。
- 插件私有 DTO 不能跨 Host 边界长期持有。
- 插件 client 的 operation 必须经过 capability 检查。

### 7. 测试策略

当前测试覆盖运行时 typed client 注册、替换、获取和移除，以及 generated descriptor 和插件撤销：

- typed client 创建。
- generated descriptor 注册。
- adapter client 进入 pipeline。
- 未声明 client 诊断。
- 插件 client 撤销。
