# AtomUI.City.Routing Guards

## Contracts

- `IRouteMatchPolicy.CanMatchAsync`: 在同模板候选中决定当前 candidate 是否可用。
- `IRouteLeaveGuard.CanLeaveAsync`: 当前 hierarchy leaf-to-root。
- `IRouteEnterGuard.CanEnterAsync`: target hierarchy root-to-leaf。

`RouteGuardResult` 支持 Allow、Reject、Cancel、Redirect 和 Failed。首个非 Allow 立即停止，snapshot/journal 不变。

`[RouteGuards]` 中实现 enter/leave 两个 contract 的类型会进入对应 descriptor 列表；只实现一个则只进入该阶段。Generator 声明中的 Behavior 必须是 public、concrete、closed class 且有 public constructor；手工 descriptor 在运行期必须引用 concrete closed implementation，并通过当前 route 的服务边界解析。

Guard 可以调用业务服务但不能操作 View/VisualTree。服务解析异常、调用异常和 null result 由导航映射为 Failed，并记录 AUCRT006；只有显式 Reject 记录 AUCRT007，Cancel/Redirect/Failed 不冒充“拒绝”；取消不记录为故障。
