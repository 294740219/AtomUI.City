# AtomUI.City.State Snapshots 合同

## 适用范围

本专题属于 `AtomUI.City.State` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Snapshots` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

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

## AtomUI.City.State 快照设计

适用范围：StateSnapshot、持久化策略、恢复、版本兼容和测试断言

### 1. 定位

`StateSnapshot` 用于保存和恢复状态，也用于测试断言和诊断。

典型用途：

- 测试断言。
- Route state 恢复。
- 应用关闭前保存 UI 状态。
- 插件状态保存。
- 调试诊断。

### 2. Snapshot 内容

Snapshot 必须包含：

- State id。
- Owner module。
- Plugin id。
- Lifetime（State 生命周期）。
- Version。
- Schema version。
- Value。
- Timestamp。

不是所有 state 都默认可持久化。需要显式声明 snapshot policy。

### 3. Snapshot Policy

应用级共享状态建议：

| 状态 | 建议 |
|---|---|
| Theme / Culture | 可持久化。 |
| Current user/auth runtime | 通常不直接持久化完整对象。 |
| Current workspace | 可持久化引用。 |
| Network status | 不持久化。 |
| Window layout policy | 可持久化。 |

策略必须说明：

- 是否持久化。
- 存储范围。
- 序列化方式。
- schema version。
- 是否允许插件迁移。

### 4. 恢复流程

恢复流程：

```text
Load snapshot
-> validate state id
-> validate owner/module/plugin
-> validate schema version
-> deserialize value
-> commit state or retain current value
```

1.0 不提供运行时 snapshot migration contract。schema version 不一致时拒绝恢复、保留当前值并记录 `AUCSTA007`。可注册迁移器属于后续版本规划，在公开迁移接口、执行顺序、失败和 AOT 合同完成前不得宣称已实现。

恢复失败不应阻止应用启动；必须保留恢复前的当前值和 version，并记录诊断。恢复逻辑不得静默覆盖启动后已经产生的运行时状态。

Transient state 不允许通过 snapshot restore 写回。恢复流程遇到非 Persisted definition 时必须保留当前值和 version，并写入 `AUCSTA007`。

### 5. 插件快照

插件 state snapshot 必须带 PluginId。

插件 state restore 必须经过：

- 插件版本兼容检查。
- 插件 schema version 检查；1.0 不兼容版本直接拒绝恢复。
- Host trust policy 检查。
- 插件已启用检查。

插件卸载后，其 snapshot 可以保留但不能被 Host 直接恢复为 Host 状态。

### 6. AOT 和 Source Generator

Generator 负责：

- 生成 snapshot serializer metadata。
- 生成 state snapshot manifest。
- 诊断不可序列化类型。
- 诊断缺少 schema version 的持久化状态。

默认禁止运行时反射发现 snapshot 类型。

### 7. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| 保存 snapshot | Unit | 输出包含 state id、version、schema。 |
| 恢复 snapshot | Unit | 值正确恢复。 |
| schema 不兼容 | Unit | 保留当前值和 version 并记录诊断。 |
| 不持久化状态 | Unit | 不写入持久化 snapshot。 |
| policy 拒绝 | Unit | Transient state restore 保留当前值并记录诊断。 |
| 插件 snapshot | Unit | 带 PluginId 和版本信息。 |
| 反射 serializer | Analyzer/Generator | Strict AOT 下诊断。 |
