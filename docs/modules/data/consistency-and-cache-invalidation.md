# AtomUI.City.Data Consistency And Cache Invalidation 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Consistency And Cache Invalidation` 相关实现决策，不重新定义模块边界。

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
| AUC-DATA-007 | Request Cache Baseline | DataRequestCacheTests |
| AUC-DATA-015 | Cache Consistency and Invalidation | DataCacheConsistencyTests; DataPluginLifecycleTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Data Consistency and Cache Invalidation 设计

适用范围：query、mutation、subscription、一致性策略、idempotency、optimistic update、rollback 和缓存失效。

### 1. 定位

Data 必须区分查询、写入和订阅。

`DataConsistencyOptions` 已实现 mutation success invalidation 与 optimistic apply/confirm/rollback；订阅来源通过 `IDataCacheInvalidator` 提交相同的显式失效合同。

查询可以缓存和重试。写入需要一致性和幂等性约束。订阅是长期数据流，需要投影和失效策略。

### 2. Operation 类型

| 类型 | 说明 |
|---|---|
| Query | 可缓存、可重试。 |
| Mutation | 默认不自动重试，除非声明幂等。 |
| Subscription | 长期推送。 |
| Upload / Download | 有进度、可取消、可恢复。 |

### 3. Mutation

Mutation 规则：

- 默认不自动 retry。
- 可以声明 idempotency key。
- success 后可以触发 cache invalidation。
- 可以声明 affected cache keys。
- 可以显式触发 State update。
- conflict 必须返回 DataError。

### 4. Optimistic Update

Optimistic update 必须显式声明。

```text
Apply optimistic state
-> execute mutation
-> success: confirm
-> failure: rollback
```

规则：

- rollback 必须可执行。
- Operation cancelled 时按策略 rollback。
- 插件 mutation 不能修改未授权 Host state。

### 5. Subscription Consistency

Subscription 推送可以用于维护本地状态投影。

规则：

- 消息必须有顺序或版本策略。
- 乱序消息需要按 policy 处理。
- 缺失消息需要重新同步或标记 stale。
- 重连后是否 replay 由 transport 和 Host policy 决定。

### 6. Cache Invalidation

失效来源：

- mutation success。
- subscription message。
- manual invalidation。
- principal change。
- plugin contribution revoked。
- route leave。
- TTL expired。

失效动作必须进入诊断。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| mutation conflict | Conflict。 |
| optimistic rollback failed | 记录 `AUCDATA032` 诊断；不覆盖 transport result。 |
| invalidation failed | 记录诊断，不吞掉 mutation result。 |
| subscription gap | 标记 stale，按策略重新同步。 |

### 8. 测试策略

AUC-DATA-015 完成时必须覆盖：

- query cache。
- mutation 不自动 retry。
- idempotency key retry。
- optimistic success / rollback。
- mutation invalidates cache。
- subscription message invalidates cache。
- principal change invalidates cache。
