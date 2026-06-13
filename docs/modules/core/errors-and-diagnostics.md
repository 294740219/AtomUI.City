# AtomUI.City.Core Errors And Diagnostics 合同

## 适用范围

本专题属于 `AtomUI.City.Core` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Errors And Diagnostics` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.Core` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-CORE-001 | Application Host Builder | ApplicationHostBuilderTests; ApplicationHostRuntimeTests |
| AUC-CORE-002 | Lifecycle Pipeline | LifecycleMiddlewarePipelineTests; ApplicationHostLifecycleIntegrationTests |
| AUC-CORE-003 | Lifecycle Scope Tree | LifecycleScopeTreeTests |
| AUC-CORE-004 | Module Contract | ModuleAttributeTests; ModuleBaseTests; ModuleDescriptorTests |
| AUC-CORE-005 | DI Registration Markers | ServiceRegistrationAttributeTests |
| AUC-CORE-006 | Host Diagnostics | HostDiagnosticsTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Core Errors and Diagnostics 设计

适用范围：`AtomUI.City.Core` 中错误分类、错误策略、诊断上下文、日志事件、错误聚合、插件错误隔离和现场排查能力。

### 1. 定位

Errors and Diagnostics 是 Core 的横切基础设施。它不替代 Logging，也不替代异常机制，而是为 Host、Lifecycle、Module、DI、Configuration、PluginSystem 提供统一的错误分类、错误处理策略和诊断上下文。

Core 必须回答三个问题：

- 当前错误是否致命。
- 出错后应该停止、降级、回滚还是继续释放。
- 现场排查需要记录哪些结构化诊断信息。

### 2. 非目标

不提供 UI 弹窗实现，不定义业务异常体系，不负责 Data/MVVM/Routing 的具体错误模型，不做远程遥测默认实现，不吞掉异常。

上层模块可以把 Core 诊断映射成 UI、日志文件、遥测事件或测试断言。

### 3. 错误分类

建议 Core 定义错误级别：

| 级别 | 含义 |
|---|---|
| `Fatal` | Host 不能继续运行。 |
| `Recoverable` | 当前操作失败，但应用可继续。 |
| `Degraded` | 能继续运行，但某个能力被禁用。 |
| `Ignored` | 已记录，可忽略。 |

建议定义错误来源：

```text
Host
Configuration
DependencyInjection
Module
Lifecycle
Contribution
Plugin
Scope
Disposal
```

取消不是错误。`OperationCanceledException` 默认映射为 `Canceled` 结果，不进入失败统计，除非取消本身违反生命周期规则。

### 4. ErrorPolicy

Core 提供默认 `ErrorPolicy`，并允许 Host 配置替换或扩展。

默认策略：

| 场景 | 默认处理 |
|---|---|
| Host 创建失败 | Fatal。 |
| Configuration required 失败 | Fatal。 |
| ModuleGraph 失败 | Fatal。 |
| 静态 Module 初始化失败 | Fatal，可配置降级。 |
| 插件加载失败 | Non-fatal，禁用插件。 |
| 插件卸载失败 | 标记 `UnloadPending`。 |
| Contribution 注册失败 | 当前模块或插件激活失败。 |
| Contribution 撤销失败 | 继续撤销剩余 Lease，最后聚合。 |
| Scope dispose 失败 | 继续释放，最后聚合。 |
| Route/Activation/Operation 失败 | 交给对应模块转换成失败结果。 |

### 5. DiagnosticContext

每个 Host、Scope、Module、Plugin、Contribution 都应能关联诊断上下文。

建议包含：

- CorrelationId。
- OperationId。
- ApplicationName。
- EnvironmentName。
- ModuleId。
- PluginId。
- ScopeId。
- ScopeKind。
- LifecyclePhase。
- ContributionId。
- ConfigurationSection。
- ServiceContextKind。
- StartTime / Duration。
- Cancellation 状态。

DiagnosticContext 必须可向子 Scope 传播，并允许中间件追加字段。

### 6. DiagnosticRecord

Core 诊断输出使用结构化 record，而不是只有字符串日志。

建议字段：

```text
Timestamp
Severity
Category
EventId
Message
Exception
DiagnosticContext
Tags
Result
Duration
```

Logging 是输出目标之一。Core 诊断记录应能进入：

- `ILogger`
- 内存诊断缓冲
- 测试诊断收集器
- 插件卸载诊断
- 后续遥测适配层

### 7. Error Pipeline

Lifecycle 已经有 middleware 机制，Error 也应走 pipeline。

```text
ErrorContext
-> Error middleware
-> Default ErrorPolicy
-> Diagnostic sink
```

Error middleware 可以：

- 转换错误级别。
- 追加诊断信息。
- 决定是否继续释放。
- 决定插件是否禁用。
- 决定是否触发 Host shutdown。

但 middleware 不能静默吞掉 Fatal 错误，除非 Host policy 明确允许降级。

### 8. 错误聚合

关闭、卸载和释放阶段必须使用错误聚合。

```text
Stop accepting new work
-> Cancel children
-> Revoke leases
-> Dispose scopes
-> Aggregate failures
-> Report final result
```

单个释放失败不能阻断后续释放。最终结果需要包含所有失败项，特别是插件卸载和 Lease 撤销。

### 9. 插件错误隔离

插件错误默认不让主应用崩溃。

规则：

- 插件加载失败：插件进入 Disabled。
- 插件激活失败：回滚已创建 Lease。
- 插件运行期失败：关闭对应 Scope 或 Operation。
- 插件卸载失败：进入 `UnloadPending`，阻止更新和删除。
- 插件异常必须带 PluginId、ModuleId、ContributionId。

### 10. AOT / Source Generator

Core runtime 不依赖反射生成诊断。

Source Generator / Analyzer 负责构建期诊断，例如：

- 模块依赖错误。
- 服务注册冲突。
- Options binding 不 AOT 友好。
- Contribution id 重复。
- 插件 manifest 不完整。

运行时 Diagnostics 负责运行期诊断，例如：

- 启动失败。
- 配置验证失败。
- 插件加载失败。
- Scope 释放失败。
- Lease 撤销失败。

### 11. 公共抽象建议

| 类型 | 职责 |
|---|---|
| `ErrorPolicy` | 错误策略。 |
| `ErrorContext` | 错误处理上下文。 |
| `ErrorHandlingResult` | 错误处理结果。 |
| `DiagnosticContext` | 当前诊断上下文。 |
| `DiagnosticRecord` | 结构化诊断记录。 |
| `IDiagnosticSink` | 诊断输出目标。 |
| `IDiagnosticContextAccessor` | 当前诊断上下文访问器。 |
| `AggregateLifecycleException` | 生命周期释放阶段聚合异常。 |

### 12. 测试策略

Testing 包应支持：

- 捕获 DiagnosticRecord。
- 断言 ErrorPolicy 结果。
- 模拟模块初始化失败。
- 模拟插件加载/卸载失败。
- 模拟 Lease 撤销失败。
- 断言释放阶段继续执行并聚合错误。
- 断言取消不被当作失败。
