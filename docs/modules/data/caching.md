# AtomUI.City.Data Caching 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Caching` 相关实现决策，不重新定义模块边界。

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
| AUC-DATA-007 | Request Cache Baseline | DataRequestCacheTests; DataCacheConsistencyTests; DataPipelineTests |
| AUC-DATA-015 | Cache Consistency and Invalidation | DataCacheConsistencyTests; DataPluginLifecycleTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data Caching 设计

适用范围：request result cache、principal 隔离、插件缓存撤销和缓存诊断。

### 1. 定位

Data cache 用于减少重复请求和提升响应速度。

线程安全内存缓存已支持 canonical identity、TTL、精确 key 与 operation/principal/permission/plugin/client-version/policy-version 等多来源批量失效。默认 pipeline/cache 通过 mutation epoch 防止失效前开始的在途 query 在失效后重新写入陈旧缓存。

Cache 不是 State。Cache 是数据访问层优化；State 是应用状态表达。Data 不应把所有响应自动写入全局 State。

### 2. 缓存类型

| 类型 | 说明 |
|---|---|
| Request result cache | pipeline query 的 typed result 缓存，可配置 TTL。 |
| Streaming / realtime snapshot | 不属于 Data 1.0 cache；由应用 adapter 或 State 显式维护。 |
| Entity cache | 不属于 Data 1.0 contract。 |

### 3. 缓存 key

缓存 key 必须包含：

- DataClientId。
- Operation name。
- Request parameters hash。
- Principal revision。
- Auth scheme。
- Permission / capability revision。
- Plugin contribution id。
- Client version。
- Cache policy version。

用户 A 的缓存不能被用户 B 读到。
`DataCacheKey` 的 required string components 必须拒绝 `null`、空字符串和空白字符串，`PluginContributionId` 可以为 `null` 但不能是空白字符串。

插件来源请求的 `PluginContributionId` 由 pipeline 从签发的 active origin 自动写入 cache key；调用方显式声明不同 contribution id 时必须在 cache lookup 前返回 `PolicyRejected`。

### 4. Streaming 和 SignalR

Streaming 和 SignalR 默认不缓存原始消息。

`DataStream`/`DataSubscription` 只提供有界 buffer 和 backpressure，不接入 `IDataRequestCache`。latest snapshot 与 state projection 由应用显式实现；Data 不提供无限消息缓存。

### 5. 失效

缓存失效来源：

- Mutation success。
- Principal change。
- Permission / capability revision change。
- Plugin contribution revoked。
- Client version changed。
- Policy version changed。
- Operation/client targeted invalidation。
- Manual invalidation。
- TTL expired。

### 6. 插件缓存

插件 client 缓存必须带全局唯一的 ContributionId；pipeline 从 Host 签发的 origin 自动绑定该值。

插件停用时：

```text
Stop new plugin data operations
-> cancel running operations
-> revoke client descriptors
-> invalidate plugin cache entries
```

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| cache read failed | 记录诊断，继续请求 transport。 |
| cache write failed | 返回请求结果，记录诊断。 |
| cache key principal/revision 为空白 | 构造时拒绝；省略 principal 时显式使用 `anonymous` 默认值。 |
| 插件缓存撤销失败 | 聚合错误，继续撤销其他资源。 |

### 8. 测试策略

当前测试覆盖精确 key、value equality、principal isolation、hit/miss、TTL 和多来源批量失效：

- cache hit / miss。
- principal 隔离。
- key required components。
- permission revision 失效。
- mutation 后失效。
- plugin cache revoke。
- 精确 key 失效的真实删除计数与 null key 拒绝。
- 在途 query 跨越 invalidation 时跳过 stale cache write。
