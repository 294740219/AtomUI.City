# AtomUI.City.Routing Navigation

## Entry Points

`IRouter` 提供 typed/untyped route reference、path、URI deep link、Back 和 Forward。所有入口进入同一事务管线。

URI deep link 使用 URI path 匹配；query 参数和 fragment 解码后加入 parameter map，path 捕获值优先。

## Options

- `Mode`: Push、Replace、Reset。
- `HistoryBehavior`: Record、Skip、ReplaceCurrent。
- `ConcurrencyPolicy`: CancelPrevious、Queue、RejectIfBusy。
- `OutletName`: path/deep-link 的目标 outlet，默认 `primary`，不能为空。
- `ForceReload`: shared route prefix 视为零，重新执行整条 target pipeline。
- `AllowRedirect`: false 时拒绝 redirect。
- `Timeout`: 正数或 infinite。
- `JournalCapacity`: 小于 1 时仅保留 current。

未知 enum 返回 `CITY-NAVIGATION-OPTIONS-INVALID`。

Journal restore marker 和 restored data 由 `NavigationScope` 内部创建，不是开发者可设置的 `NavigationOptions`。

## Redirect

Static redirect 来自 Redirect route；dynamic redirect 来自 Guard/Resolver result。Static redirect 也执行 match policy，并保留 deep-link query/fragment；path 捕获值覆盖同名 query。重定向沿用 operation id、原始 options/outlet，并累积 parameters；较新的 redirect 显式参数覆盖同名继承参数。循环检测使用结构化 target 身份，不拼接可碰撞的字符串；最多 8 次。最终 Redirected result 包含 final route 和 parameters。Redirect descriptor 只能指向可导航的 Route/Layout/Index/Redirect，不能指向 Group 或 ExtensionPoint。

## Transaction

Commit 是一次不可分割的 `NavigationSnapshot` 与 journal 更新。核心管线先 prepare，整个 middleware 调用栈正常退出后才 commit。每层 middleware 都拥有独立的 `next` 一次性调用边界；已启动但未 await 的 `next` 仍会在 transaction gate 内被等待，重复调用或该层返回后再调用都会失败。返回结果的 operation id/target 必须与当前事务一致。NotFound、Rejected、Cancelled、Failed、middleware post-`next` 失败、无效 middleware result 和仅发出 redirect 的中间结果都不提交。

只有 caller、timeout、CancelPrevious 或 scope disposal 实际取消 navigation token 时，取消异常才产生 Cancelled。Guard、Resolver 或 Middleware 主动抛出与该 token 无关的 `OperationCanceledException` 属于组件失败。

Routing 不执行 ViewModel 或 UI commit。
