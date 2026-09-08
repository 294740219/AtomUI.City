# AtomUI.City.Routing Detailed Design

本文是 [architecture.md](architecture.md) 的展开索引，不再保存历史方案。发生冲突时，以 [api-contracts.md](api-contracts.md) 和源码测试为准。

## Pipeline

1. Capture immutable graph。
2. Match/bind。
3. Run match policies。
4. Enter middleware。
5. Run leave guards。
6. Run enter guards。
7. Run resolvers。
8. Prepare immutable navigation snapshot and success result。
9. Exit middleware。
10. Publish snapshot and journal only when the complete middleware chain returns the prepared result。

每一层 middleware 的 `next` 都只能在该层调用窗口内启动一次；terminal 另有同样的一次性边界。任何已启动的 downstream task 即使未被 middleware await，也必须在 transaction gate 释放前完成；窗口关闭后的延迟调用不能重新进入下游 pipeline。最终 result 的 operation id 和 target 必须属于当前事务。

## Boundaries

- Routing target 是 `ViewModelTargetDescriptor`，不是实例。
- Routing transaction 不包含 ViewModel creation、ActivationScope、DI RouteScope、Outlet 控件或 VisualTree；named outlet route selection 属于 Routing。
- Presentation 可基于成功 snapshot 执行自己的 UI transaction。
- PluginSystem 在 route lease revoke 前负责 drain。
- Data/Security/Localization 通过注入的 Resolver/Guard/metadata 集成。

## Failure Discipline

Graph candidate 失败不替换 published graph。Navigation pipeline 失败不替换 current snapshot。Journal mutation 只发生在 commit。只有 navigation token 已发出取消时，`OperationCanceledException` 才映射为 Cancelled；用户组件无关的取消异常映射为 Failed。诊断失败不替代原始结果。Dispose 等待外部代码退出；same-chain reentrancy 明确拒绝。

各专题的完整合同见 overview 文档索引。
