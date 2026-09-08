# AtomUI.City.State Collection State 合同

## 适用范围

本专题属于 `AtomUI.City.State` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Collection State` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.State 集合状态设计

适用范围：keyed collection state、集合变更、item 版本、快照和不可变更新

### 1. 定位

集合状态使用 .NET 风格命名：

```text
IStateCollection<TKey, TItem>
```

它用于表示需要增量变化通知、快照和诊断的 keyed collection state。

### 2. 能力

第一版能力：

- 按 key 添加或更新。
- 按 key 删除。
- 清空。
- 查询只读快照。
- 发出集合级变更通知。
- 支持 item 级版本。
- 支持 snapshot。
- 支持 `IStateCollection<TKey,TItem>` / `StateCollection<TKey,TItem>` dispose 生命周期。
- 在集合快照、快照条目、变更记录和事件参数构造边界拒绝非法输入。

不建议直接暴露可变 `List<T>` 或 `Dictionary<TKey,T>`。

### 3. 更新规则

集合变更必须通过状态 API，以便触发通知、诊断和快照。

规则：

- 不允许外部拿到可变内部集合。
- item 更新必须产生明确 change record。
- 相同 key 的变更保持顺序。
- 批量更新应合并通知。
- 批量输入中的重复 key 合并为该 key 的最终值，变更顺序按 key 首次出现顺序确定。
- restore 的内容和 item version 全部相同时，仅 snapshot collection version 不同不构成变更，不修改当前 collection version，也不通知。
- 更新失败时保留旧集合。
- public contract 载体必须在进入恢复或通知链路前拒绝 null key、null 条目、未知 change kind 和负 version。
- 集合 Dispose 必须幂等，且释放现有 subscriptions。
- Dispose 后 `Version`、`Items`、`TryGetItemVersion` 和 `CreateSnapshot` 仍可读取。
- Dispose 后 `AddOrUpdate`、`AddOrUpdateRange`、`Remove`、`Clear`、`RestoreSnapshot` 和 `OnChange` 必须抛 `ObjectDisposedException`。
- 具体类型的 `Changed` 是同步 CLR event；`OnChange` 是支持调度和 Scope 释放的受管订阅。一次集合变更先调用 `Changed`，再投递 `OnChange` subscriptions。

### 4. 变更记录

集合变更记录应表达：

- Added。
- Updated。
- Removed。
- Cleared。
- Reset。

每条记录包含 key、旧值、新值、集合版本和 item 版本。

变更记录合同：

- `Kind` 必须是 `Added`、`Updated`、`Removed`、`Cleared` 或 `Reset`。
- `Key` 不得为 null。
- `CollectionVersion` 和 `ItemVersion` 必须大于等于 0。
- `StateCollectionChangedEventArgs<TKey,TItem>` 的 change 列表不得为 null、空列表或包含 null 项。
- 单项 change 构造参数为 null 时必须抛 `ArgumentNullException`。

### 5. Snapshot

当前运行时集合 snapshot 包含：

- `CollectionVersion`。
- item count。
- item 列表。
- 每个 item 的 key、value 和 item version。

运行时 snapshot 合同：

- `CollectionVersion` 必须大于等于 0。
- snapshot item 列表不得为 null，且不得包含 null 项。
- snapshot item 的 key 不得为 null。
- snapshot item version 必须大于等于 0。
- snapshot 和事件参数暴露的列表创建后不可变，外部修改输入列表不得影响内部结果。

后续持久化 snapshot 格式必须补充：

- collection key。
- schema version。
- serialized items。

大型集合应支持分页或分块 snapshot，避免一次性占用过多内存。

### 6. AOT 和 Source Generator

Generator/Analyzer 负责：

- 生成 collection descriptor。
- 生成 item serializer metadata。
- 诊断可变集合直接暴露。
- 诊断缺少 key comparer。
- 诊断 snapshot item 不可序列化。

### 7. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| AddOrUpdate | Unit | 添加和更新产生正确 change record。 |
| Remove | Unit | 删除后快照不包含 item。 |
| Clear | Unit | 清空产生 clear 记录。 |
| 只读快照 | Unit | 外部不能修改内部集合。 |
| 批量更新 | Unit | 通知合并且顺序稳定。 |
| 批量重复 key | Unit | 合并为最终值，每个 key 只产生一条 change。 |
| 仅快照版本不同 | Unit | 不修改当前 version，不通知并返回 false。 |
| item version | Unit | item 更新递增版本。 |
| collection snapshot | Unit | 保存和恢复集合。 |
| collection dispose | Unit | 重复 Dispose 幂等，Dispose 后读 API 可用。 |
| disposed collection mutation | Unit | Dispose 后 mutation、restore 和 subscription API 抛 `ObjectDisposedException`。 |
| disposed collection subscriptions | Unit | Dispose 清空 active subscriptions，subscription 重复 Dispose 幂等。 |
| snapshot 合同 | Unit | 拒绝负 collection version、null item、null key 和负 item version。 |
| change 合同 | Unit | 拒绝未知 change kind、null key 和负 version。 |
| event args 合同 | Unit | 拒绝空 change 列表和 null change 条目。 |
