# AtomUI.City.Routing Route Graph

## Snapshot

`RouteGraphSnapshot` 保存 version、只读 descriptor 列表、route id、parent/children、contribution 和 matcher 索引。所有集合均为只读副本。

Graph build 验证已知 route kind、id、template、Group template、parent cycle、effective full-template conflict、index、redirect、extension point、behavior contract 和 version。等价路径按完整 parent 组合路径与 outlet 比较；允许多个带 MatchPolicy 的候选，但最多一个无条件 fallback。失败抛 `RouteGraphException`，其 `Error` 是稳定 `RouteGraphError`。

## Registry

`RouteRegistry` 是线程安全 singleton 和 `IRouteGraphProvider`。导航只读取 `CurrentSnapshot`。Add/Remove 在锁内构建并发布候选，在锁外写诊断。

```csharp
var lease = registry.AddContribution(new RouteContribution(
    "plugin.profile",
    generatedRoutes,
    type => pluginServices.GetService(type)));
```

Contribution 至少包含一个非 null route；相同 contribution id 不能重复激活。`RouteContribution` 在所有权边界为 `ContributionId == null` 的 generated/application descriptor 创建带 contribution id 的不可变副本，因此 generated descriptors 可直接使用；已经属于其他 contribution 的 descriptor 被拒绝。进入 `RouteGraphSnapshot.WithContribution` 的所有 descriptor 必须拥有匹配 id，因此不存在只有 service resolver、无法撤销的空贡献。

## ExtensionPoint

Host 用 ExtensionPoint descriptor 声明挂载点及所属 parent。Contribution route 通过 `ExtensionPoint` id 挂载时，Registry 将其有效 parent 设为该扩展点 parent。missing point 或显式 parent 不一致会拒绝整个 candidate。

Route id、parent id、contribution id、extension point 和 outlet name 使用 ordinal、大小写敏感的身份比较；template literal 匹配大小写不敏感。Graph 使用结构化 parent/outlet/template key，不使用可发生分隔符碰撞的拼接字符串。

## Publication

- 成功 Add/Remove 创建 `Version + 1` snapshot。
- 失败不改变当前 snapshot 或 service resolver map。
- 旧 snapshot 仍可被正在运行的导航读取。
- Lease revoke 失败可在先移除依赖 contribution 后重试。
