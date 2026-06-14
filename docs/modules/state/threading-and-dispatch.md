# AtomUI.City.State Threading And Dispatch 合同

## 适用范围

本专题属于 `AtomUI.City.State` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Threading And Dispatch` 相关实现决策，不重新定义模块边界。

## 设计决策

- 默认不隐式切线程。
- 后台任务必须观察 cancellation。
- UI 更新必须进入 Presentation dispatcher。

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

## AtomUI.City.State 线程与调度设计

适用范围：状态提交、通知调度、Core Threading 集成、多线程约束和 UI Dispatcher 边界

### 1. 定位

桌面软件是天然多线程环境。State 模块必须在内核线程模型下运行，不能让状态提交、订阅通知和 UI 更新互相污染。

线程模型见：[Core Threading 设计](../core/threading.md)。

### 2. 基本规则

State 必须满足：

- `SetValue` 和 `Update` 原子化。
- 状态提交和订阅通知分离。
- 不在状态锁内调用订阅者。
- 相同 state key 的变更通知保持顺序。
- 应用级共享状态绑定 ApplicationScope。
- 插件状态绑定插件生命周期或插件贡献 lease。
- 推荐状态值使用 immutable 或 replace-only 风格。

### 3. 提交流程

```text
SetValue / Update
-> acquire state mutation gate
-> compare value
-> commit value and version
-> create change record
-> release mutation gate
-> dispatch notifications
```

订阅者运行在锁外，避免死锁和重入污染。

### 4. 调度策略

State Core 不直接依赖 Avalonia Dispatcher。

调度策略：

| 策略 | 说明 |
|---|---|
| Immediate | 当前线程通知。 |
| Queued | 排队后统一通知。 |
| Dispatcher | 切到 UI dispatcher。 |
| Background | 后台投递；不得阻塞状态提交，handler 失败必须写 diagnostics。 |

延迟调度回调必须在实际执行前重新检查 subscription 是否已 Dispose，避免 UI dispatcher 队列中的旧通知更新已经释放的 owner。

Presentation 负责把 Dispatcher 接入 State。Core 只定义抽象。

### 5. UI 边界

State Core 不直接更新 UI。

```text
State change committed
-> DispatchPolicy.UiThread
-> Presentation dispatcher
-> ViewModel property change or binding refresh
-> AtomUI/Avalonia visual refresh
```

UI 订阅必须绑定 Scope。View detached 后，相关 UI 订阅应停止更新。

### 6. Late Result Suppression

OperationScope 取消后不应继续提交状态更新。

规则：

- Data 请求完成前如果 OperationScope 已取消，结果必须被忽略或记录为 late result。
- Command 取消后不提交成功状态。
- RouteScope 离开后，Resolver 的 late result 不应更新旧路由状态。

### 7. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| 原子提交 | Unit | 并发更新不破坏值和 Version。 |
| 锁外通知 | Unit | handler 重入不会死锁。 |
| 顺序通知 | Unit | 同一 key 通知顺序稳定。 |
| UI 调度 | Unit | fake dispatcher 收到 UI 订阅。 |
| UI 调度不可用 | Unit | 不可用 dispatcher 不回滚状态提交，handler 不执行，诊断包含 dispatcher type。 |
| UI 延迟回调释放 | Unit | Dispatcher pending callback 在 subscription Dispose 后不执行 handler。 |
| Background 调度 | Unit | handler 阻塞时 SetValue 先返回，handler 失败写 diagnostics。 |
| Scope 停用 | Unit | 停用后不再投递 UI 更新。 |
| late result | Unit | Operation 取消后不提交状态。 |
