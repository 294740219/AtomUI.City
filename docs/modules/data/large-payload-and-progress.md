# AtomUI.City.Data Large Payload And Progress 合同

## 适用范围

本专题属于 `AtomUI.City.Data` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Large Payload And Progress` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Data Large Payload and Progress 设计

适用范围：上传、下载、大载荷、进度、range、临时文件、节流、取消和内存约束。

### 1. 定位

桌面应用经常处理大文件、内网资源、报表、导入导出和长时间下载。Data 必须支持大载荷和进度，而不是把所有内容一次性读入内存。

### 2. Upload / Download

Data operation 可以声明：

- Upload。
- Download。
- Download to stream。
- Download to temporary file。
- Range request。
- Resumable transfer。

### 3. 进度模型

进度事件应包含：

- OperationId。
- Bytes transferred。
- Total bytes if known。
- Percent if computable。
- Speed estimate。
- Stage。
- CancellationToken。

进度通知必须节流。

### 4. 内存约束

规则：

- 大文件默认流式处理。
- 不默认把完整 payload 放入内存。
- 临时文件必须绑定 OperationScope。
- Operation 取消时清理临时文件。
- 插件上传下载不能把 Host stream 长期保存。

### 5. UI 线程

进度回调不能直接更新 UI。

```text
Transport progress
-> Data progress dispatcher
-> throttled progress state
-> Presentation binding / State subscription
```

### 6. 错误策略

| 场景 | 默认处理 |
|---|---|
| 用户取消 | Cancelled，清理临时文件。 |
| disk full | LocalStorageError。 |
| network lost | NetworkUnavailable。 |
| range unsupported | Fallback 或 Failed，按 policy。 |
| progress handler failed | 记录诊断，不杀死 transport。 |

### 7. 测试策略

测试必须覆盖：

- upload progress。
- download progress。
- cancellation cleanup。
- range request。
- large payload streaming。
- progress throttle。
- disk error。
