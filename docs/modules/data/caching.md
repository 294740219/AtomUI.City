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

## AtomUI.City.Data Caching 设计

适用范围：request cache、response cache、snapshot cache、principal 隔离、插件缓存撤销和缓存诊断。

### 1. 定位

Data cache 用于减少重复请求和提升响应速度。

Cache 不是 State。Cache 是数据访问层优化；State 是应用状态表达。Data 不应把所有响应自动写入全局 State。

### 2. 缓存类型

| 类型 | 说明 |
|---|---|
| Request cache | 同一请求 key 的短期结果缓存。 |
| Response cache | HTTP response 或 transport response cache。 |
| Snapshot cache | streaming / realtime 的 latest snapshot。 |
| Entity cache | 可选高风险能力，第一版只定义扩展点。 |

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

### 4. Streaming 和 SignalR

Streaming 和 SignalR 默认不缓存原始消息。

允许：

- latest snapshot。
- bounded buffer。
- explicit state projection。

不允许无限消息缓存。

### 5. 失效

缓存失效来源：

- Mutation success。
- Principal change。
- Permission / capability revision change。
- Plugin contribution revoked。
- Client version changed。
- Manual invalidation。
- TTL expired。

### 6. 插件缓存

插件 client 缓存必须带 PluginId 和 ContributionId。

插件停用时：

```text
Stop new plugin data operations
-> cancel running operations
-> revoke client descriptors
-> invalidate plugin cache entries
-> dispose cache handles
```

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| cache read failed | 记录诊断，继续请求 transport。 |
| cache write failed | 返回请求结果，记录诊断。 |
| cache key 缺少 principal | 拒绝缓存。 |
| 插件缓存撤销失败 | 聚合错误，继续撤销其他资源。 |

### 8. 测试策略

测试必须覆盖：

- cache hit / miss。
- principal 隔离。
- permission revision 失效。
- mutation 后失效。
- plugin cache revoke。
- streaming snapshot cache。
