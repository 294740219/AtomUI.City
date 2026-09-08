# AtomUI.City.Routing Resolvers

## Contract

`IRouteResolver.ResolveAsync(RouteResolveContext, CancellationToken)` 在 commit 前准备 target 所需只读数据。每次普通导航都对完整 target hierarchy 按 root-to-leaf、同 route 声明顺序串行执行；不能因 route prefix 复用而丢失父层 data。只有带已保存标量 data 的 journal restore 跳过 resolver。

Result：Success(data)、NotFound、Redirect、Cancelled、Failed。重复 data key 是确定性失败 `CITY-NAVIGATION-RESOLVER-DUPLICATE-KEY`。

Success data 复制到 `NavigationSnapshot.ResolvedData`，与 route/parameters/version 原子发布。失败不改变旧 snapshot。Routing 不把 data 注入或创建 ViewModel；Presentation/应用读取 snapshot 并完成后续激活。

Resolver 从应用 DI scope解析；contribution route 优先使用其 `RouteContribution.ServiceResolver`。取消前后均检查 token。

Resolver service 解析、调用及 null result 都属于 resolver stage；失败记录 AUCRT006，不会被外层 middleware 错误归因。`NotFound` 表示 route 已匹配但所需数据不存在，因此映射为 navigation `Failed`，而不是重新进行 route matching。

Journal 只保留 null/string/数值/bool/char/Guid/日期时间/TimeSpan 标量。只有一次 resolution 的全部 data 都可安全保存时，返回 route 才跳过 resolver；任意 DTO 或插件私有对象都会使该 entry 在返回时重新运行完整 resolver hierarchy。
