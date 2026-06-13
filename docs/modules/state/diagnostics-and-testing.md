# AtomUI.City.State Diagnostics And Testing 合同

## 适用范围

本专题属于 `AtomUI.City.State` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Diagnostics And Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

## Public Contract

- 只允许通过 `AtomUI.City.State` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-STATE-001 | Writable State | WritableStateTests |
| AUC-STATE-002 | Application State | ApplicationStateTests |
| AUC-STATE-003 | Computed State | ComputedStateTests |
| AUC-STATE-004 | State Subscription | StateScopeTests; StateThreadingTests |
| AUC-STATE-005 | State Snapshot | StateSnapshotTests |
| AUC-STATE-006 | Collection State | StateCollectionTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.State 诊断与测试设计

适用范围：State 诊断字段、错误码、测试工具、测试矩阵和完成门禁

### 1. 定位

State 的每个功能点必须可测试。涉及生命周期、线程、插件和释放的功能点必须有释放断言。

### 2. 诊断字段

State 诊断至少包含：

- StateKey。
- State type。
- Owner module。
- PluginId。
- ScopeId。
- Version。
- OperationId。
- DispatchPolicy。
- Error code。
- Snapshot schema version。

### 3. 错误策略

| 场景 | 默认处理 |
|---|---|
| Set/Update 失败 | 保留旧值，记录诊断。 |
| 应用级状态未注册 | 返回诊断错误，不创建隐式全局状态。 |
| 应用级状态未授权写入 | 拒绝写入，记录诊断。 |
| Computed 计算失败 | 保留上一有效值或标记 failed，记录诊断。 |
| Subscription 失败 | 记录错误，不杀死 state。 |
| Snapshot 保存失败 | 当前保存失败，不影响运行 state。 |
| Snapshot 恢复失败 | 使用默认值，记录诊断。 |
| Plugin state 释放失败 | 进入插件卸载错误聚合。 |

取消不是错误。OperationScope 取消后不应继续提交状态更新。

### 4. 测试工具

Testing 包应支持：

- 创建 TestStateScope。
- 创建 IReadOnlyState / IWritableState。
- 注入测试 `IApplicationState`。
- 断言应用级状态读写。
- 断言应用级状态访问策略。
- 断言 Version。
- 断言 OnChange 通知。
- 断言相等值不通知。
- 断言 computed cache / invalidation。
- 断言 subscription 自动释放。
- 断言 snapshot 保存和恢复。
- 断言插件 state 卸载。
- 断言调度策略。

### 5. 模块测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| state set/get | Unit | 值和版本正确。 |
| 相等值不通知 | Unit | 不递增版本，不触发通知。 |
| computed invalidation | Unit | 依赖变化后重新计算。 |
| subscription dispose | Unit | 释放后不再通知。 |
| Scope stop | Unit | Scope 停止后不再通知。 |
| application state DI | Unit | 可通过 DI 读取和写入授权状态。 |
| access policy | Unit | 未授权写入被拒绝。 |
| dispatch policy | Unit | fake dispatcher 可确定推进。 |
| snapshot save/restore | Unit | 版本、schema 和值正确。 |
| plugin cleanup | Unit | 插件停用释放订阅和状态。 |
| source generator manifest | Generator | 输出稳定 state descriptor。 |
| analyzer diagnostics | Analyzer | 未绑定 Scope、动态发现、泄漏等诊断稳定。 |
