# AtomUI.City.State State Values 合同

## 适用范围

本专题属于 `AtomUI.City.State` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `State Values` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.State 状态值设计

适用范围：`IReadOnlyState<T>`、`IWritableState<T>`、状态版本、相等比较、原子更新和状态定义

### 1. 定位

状态值是 State 模块的最小运行单元。

State 不把状态做成静态全局变量，也不要求开发者使用 Web 风格的 Store、Signal、Action 或 Reducer。第一版使用符合 .NET 习惯的强类型状态对象：

```text
IReadOnlyState<T>
IWritableState<T>
StateKey<T>
StateDefinition<T>
```

### 2. IReadOnlyState<T>

建议语义：

```csharp
public interface IReadOnlyState<T>
{
    T Value { get; }

    long Version { get; }

    IStateSubscription OnChange(Action<StateChangedEventArgs<T>> handler);
}
```

规则：

- `Value` 表示当前已提交值。
- `Version` 每次有效变更递增。
- 相等值不触发变更。
- 变化通知在状态提交后触发。
- 订阅必须可释放。
- 订阅释放后不再收到后续通知，重复释放幂等。
- 默认不暴露 Rx 类型。

### 3. IWritableState<T>

建议语义：

```csharp
public interface IWritableState<T> : IReadOnlyState<T>
{
    event EventHandler<StateChangedEventArgs<T>>? Changed;

    bool SetValue(T value);

    bool Update(Func<T, T> updater);

    void Set(T value);
}
```

`Changed` 是低层同步 CLR event，用于 .NET 事件互操作；`OnChange` 是推荐的受管订阅入口，返回 `IStateSubscription` 并支持调度与 Scope 释放。一次提交先在当前线程调用 `Changed`，再投递 `OnChange` subscriptions。

规则：

- `SetValue` 直接设置新值。
- `Update` 基于旧值计算新值。
- 返回 `false` 表示值未变化。
- 更新必须原子化。
- 更新失败时保留旧值。
- 写入策略拒绝时保留旧值和 version。
- updater 中禁止执行 IO 或长耗时逻辑。

`WritableState<T>` 作为当前具体实现同时实现 `IDisposable`，但不把 `IDisposable` 加到 `IWritableState<T>` 合同上。Dispose 行为：

- `Dispose` 幂等，释放后清空已注册 subscriptions。
- `Value`、`Version` 和 `ValueType` 在 Dispose 后仍可读取。
- `Set`、`SetValue`、`Update` 和 `OnChange` 在 Dispose 后抛 `ObjectDisposedException`。
- 内部 restore-style mutation 在 Dispose 后也必须拒绝。
- 构造时可绑定 `stateName` 和 `StateAccessPolicy`；`ReadOnly` 状态拒绝 `Set`、`SetValue` 和 `Update`，抛 `StateAccessDeniedException` 并写 `AUCSTA004`。
- access 拒绝必须发生在 `Update` 的 updater 执行前。

异步请求不直接进入 state。异步请求属于 Data、Command 或 OperationScope，完成后再提交状态更新。

### 4. StateKey<T>

应用级和模块级共享状态必须使用强类型 key。

```csharp
public readonly record struct StateKey<T>(string Name);
```

模块声明状态 key：

```csharp
public static class ThemeStates
{
    public static readonly StateKey<ThemeMode> CurrentTheme =
        new("AtomUI.City.Theme.Current");
}
```

命名规则：

- key 必须稳定。
- key 必须可诊断。
- 插件 key 必须带插件或 package 前缀。
- 不允许不同类型复用同一个 key 名称。

### 5. StateDefinition<T>

状态注册必须显式声明定义。

```csharp
context.States.Add(
    StateDefinition.Create(
        ThemeStates.CurrentTheme,
        defaultValue: ThemeMode.System,
        lifetime: StateLifetime.Application,
        access: StateAccessPolicy.HostWrite));
```

定义内容：

- Key。
- 默认值。
- 生命周期。
- Owner module。
- Plugin id。
- 访问策略。
- 快照策略。
- 相等比较策略。
- 诊断元数据。

### 6. 相等比较

相等值不触发通知。

默认策略：

- 值类型使用默认 equality。
- 引用类型使用 `EqualityComparer<T>.Default`。
- 集合状态不能依赖可变集合引用相等。
- 需要深比较时必须显式声明 comparer。

不允许通过原地修改可变对象绕过状态提交。推荐状态值使用 immutable 或 replace-only 风格。

### 7. 原子更新

`SetValue` 和 `Update` 必须满足：

- 状态提交原子化。
- 版本递增和当前值替换不可分离。
- 不在状态锁内调用订阅者。
- 订阅者观察到通知时，`Value` 和 `Version` 必须已经是提交后的值。
- 更新失败不改变当前值。
- 写入策略拒绝不改变当前值，也不执行 updater。
- 取消后的 OperationScope 不应继续提交状态更新。

### 8. AOT 和 Source Generator

Source Generator 负责生成：

- state key manifest。
- state definition descriptor。
- snapshot serializer metadata。
- 重复 key 诊断。
- 不可序列化 snapshot 类型诊断。

默认禁止运行时扫描程序集找状态定义。

### 9. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| 读取当前值 | Unit | 初始值和更新后值正确。 |
| SetValue | Unit | 值变化时返回 true，Version 递增。 |
| 相等值提交 | Unit | 返回 false，不递增 Version，不通知。 |
| Update 原子性 | Unit | updater 成功才替换值。 |
| 提交后通知 | Unit | handler 中读取到已提交的 Value 和 Version。 |
| subscription dispose | Unit | 释放后不再收到通知。 |
| WritableState Dispose | Unit | 读属性保留，mutation 和 subscription API 抛 ObjectDisposedException，重复 Dispose 幂等。 |
| updater 异常 | Unit | 旧值保留，诊断记录。 |
| 重复 StateKey | Analyzer/Generator | 输出稳定诊断。 |
| 不可序列化 snapshot 类型 | Analyzer/Generator | 输出构建期诊断。 |
