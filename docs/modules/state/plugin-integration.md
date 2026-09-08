# AtomUI.City.State Plugin Integration 合同

## 适用范围

本专题属于 `AtomUI.City.State` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Plugin Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。

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

## AtomUI.City.State 插件集成设计

适用范围：插件状态隔离、状态写入授权、状态撤销、快照迁移和卸载安全

### 1. 定位

插件可以使用 State，但插件状态必须可隔离、可撤销、可释放、可诊断。

插件不能通过 State 绕过 Host 生命周期和权限边界。

### 2. 插件状态创建

插件 state 创建在：

- 插件服务上下文。
- 插件贡献产生的 Scope。
- 插件拥有的 RouteScope 或 ActivationScope。

插件默认只能注入只读 `IApplicationState`。

### 3. 写入授权

插件需要写入应用级状态时必须满足：

- 插件 manifest 声明 capability。
- Host 授权 capability。
- 目标状态允许 `AuthorizedWrite` 或 `PluginIsolated`。
- 写入过程写入诊断。

即使暴露 writer，也必须经过 `StateAccessPolicy` 检查。

Host 必须通过 `ApplicationStateRegistry.CreateWriter(StateWriteAuthority.Plugin(...))` 向插件提供受约束 writer。`PluginIsolated` 比较 writer plugin id 与 definition plugin id；`AuthorizedWrite` 比较 writer 已授予 capabilities 与 definition `writeCapability`。

该检查用于可信进程内扩展的策略、诊断和误用防护，不是安全沙箱。插件若能直接取得 `ApplicationStateRegistry`，便处于 Host 信任边界内；不可信插件必须采用进程隔离。

### 4. 泄漏约束

禁止：

- 插件把内部 state 实例暴露给 Host 长期持有。
- Host 静态缓存插件私有 state 类型。
- 插件 subscription 脱离插件生命周期。
- 插件 state 使用全局静态变量保存当前值。

### 5. 停用和卸载

插件停用流程：

```text
Stop new plugin state access
-> cancel plugin operations
-> dispose plugin subscriptions
-> snapshot plugin state if policy allows
-> revoke state contributions
-> release plugin state registry
```

释放失败进入插件卸载错误聚合。

### 6. Snapshot 和迁移

插件 state snapshot 必须带：

- PluginId。
- Plugin version。
- State schema version。
- Owner module。

恢复前必须检查插件版本兼容。1.0 对 schema 或插件版本不兼容的快照直接拒绝恢复并保留当前值；迁移器合同属于后续版本规划。

### 7. AOT 和 Source Generator

Generator/Analyzer 负责：

- 生成插件 state descriptor。
- 生成插件 snapshot manifest。
- 诊断插件私有类型泄漏。
- 诊断未绑定插件生命周期的 state。

### 8. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| 插件只读状态访问 | Unit | 默认只读。 |
| 插件授权写入 | Unit | capability 允许后可写。 |
| 未授权写入 | Unit | 拒绝并记录诊断。 |
| 插件停用释放 | Unit | subscriptions 和 registry 被释放。 |
| 插件 snapshot | Unit | 带 PluginId 和版本。 |
| 插件 state 泄漏 | Analyzer/Generator | 输出稳定诊断。 |
