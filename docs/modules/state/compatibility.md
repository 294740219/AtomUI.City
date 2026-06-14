# AtomUI.City.State Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 状态写入先完成原子提交，再通知订阅者。
- 默认不隐式切 UI 线程。
- StateSnapshot 创建后不可变。
- ComputedState 不能形成循环依赖。
- 插件 state definition、subscription 和 snapshot provider 必须绑定插件 owner。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `WritableState<T>` 的 optional `stateName`、`access` 构造参数、ReadOnly 写拒绝、`AUCSTA004` 诊断、原子提交后通知、相等值不通知和 Dispose 后 mutation 拒绝进入 1.0 兼容承诺。
- `ApplicationStateRegistry` 的显式注册、未注册诊断、重复注册诊断、读写入口分离、ReadOnly 写拒绝、Update null updater 优先拒绝和 `StateDefinition<T>` enum/schema 边界进入 1.0 兼容承诺。
- `ComputedState<T>` 的 lazy invalidation、缓存、依赖变更通知、compute 失败诊断、Dispose 后读取拒绝、null dependency 先校验后订阅和依赖订阅释放进入 1.0 兼容承诺。
- `StateSubscriptionOptions` 的 Immediate、Dispatcher、Background、Queued 调度语义、subscription Dispose 幂等、延迟回调释放抑制、handler 失败诊断和 StateScope 反向释放进入 1.0 兼容承诺。
- `StateSnapshot` 和 `StateSnapshotEntry` 的不可变 entries、version/schema 边界、Persisted 过滤、restore 诊断、owner/plugin/type/schema 校验和 Transient restore 拒绝进入 1.0 兼容承诺。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
