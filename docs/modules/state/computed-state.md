# AtomUI.City.State Computed State 合同

## 适用范围

本专题属于 `AtomUI.City.State` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Computed State` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.State 计算状态设计

适用范围：派生状态、依赖声明、缓存、失效、错误处理和 AOT 约束

### 1. 定位

`IComputedState<T>` 表达由一个或多个状态派生出来的只读状态。

```text
Dependencies
-> Compute
-> Cache value
-> Invalidate on dependency change
-> Notify subscribers
```

计算状态用于减少 ViewModel 中重复派生属性和手工通知逻辑。

### 2. API 语义

计算状态是只读状态。

```csharp
public interface IComputedState<T> : IReadOnlyState<T>
{
}
```

计算函数不应执行 IO，也不应启动异步任务。异步结果应先进入 Data 或 OperationScope，再提交普通状态。

### 3. 依赖声明

依赖必须显式声明或由 source generator 静态分析。

允许：

```text
ComputedState
  Dependencies:
    ThemeStates.CurrentTheme
    AuthStates.CurrentPrincipal
```

默认不允许：

- 运行时反射扫描依赖。
- expression-tree 依赖分析作为默认路径。
- 通过闭包捕获未知状态对象。

### 4. 缓存和失效

规则：

- 计算结果应缓存。
- 依赖变化后标记失效。
- 有订阅或读取时才重新计算。
- 计算结果相等时不通知。
- 依赖通知顺序应保持确定性。
- 依赖列表不能包含 null 项。
- 构造函数必须先校验完整依赖列表，再建立订阅；遇到 null dependency 时不能留下半初始化订阅。
- 无订阅者时依赖变化只标记 dirty，不立即运行计算函数。

### 5. 错误策略

计算异常不能杀死依赖状态。

默认处理：

- 保留上一有效值，或进入 failed 状态。
- 记录 Diagnostics。
- 通知订阅者计算失败状态。
- 不重复无限重算同一个失败状态。

### 6. 生命周期

计算状态绑定创建它的 StateScope。

规则：

- Scope 停止后不能继续计算。
- 依赖订阅随计算状态释放。
- 订阅阶段部分失败时，已建立的依赖订阅必须释放。
- 插件计算状态不能被 Host 长期持有。
- RouteScope 或 ActivationScope 中的计算状态随对应 Scope 释放。

### 7. AOT 和 Source Generator

Generator 负责：

- 生成 computed descriptor。
- 生成依赖列表。
- 诊断无法静态分析的依赖。
- 诊断 computed 捕获插件私有类型泄漏到 Host。

Analyzer 应提示：

- 计算函数执行 IO。
- 计算函数返回可变集合且未声明比较策略。
- 计算状态缺少生命周期绑定。

### 8. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| 初次计算 | Unit | 首次读取返回计算值。 |
| 缓存 | Unit | 依赖未变时不重复计算。 |
| 依赖失效 | Unit | 依赖变化后重新计算。 |
| Lazy invalidation | Unit | 无订阅者时依赖变化不立即重算，下一次读取才重算。 |
| 相等结果 | Unit | 结果相等时不通知。 |
| 计算异常 | Unit | 保留旧值或 failed 状态，诊断记录。 |
| null dependency | Unit | 构造函数拒绝 null dependency。 |
| Scope 释放 | Unit | 释放后不再计算或通知。 |
| 依赖无法静态分析 | Analyzer/Generator | 输出稳定诊断。 |
