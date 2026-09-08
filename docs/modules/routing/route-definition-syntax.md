# AtomUI.City.Routing Route Definition Syntax

## Route Map

```csharp
[RouteMap]
public static partial class AppRoutes
{
    [LayoutRoute(typeof(ShellViewModel), Id = "app.shell")]
    public static partial RouteReference Shell();

    [Route("details/{id:int:min(1)}", typeof(DetailsViewModel),
        Id = "app.details", Parent = nameof(Shell), ReuseKey = "details:{id}")]
    [RouteGuards(typeof(DetailsGuard))]
    [RouteResolvers(typeof(DetailsResolver))]
    [RouteMiddleware(typeof(DetailsMiddleware))]
    public static partial RouteReference<DetailsParameters> Details();
}
```

Map 必须是非泛型 top-level public static partial class。方法必须是非泛型 public static partial、无参数、无手写实现，并且恰好声明一个 route-definition attribute。普通方法返回 `RouteReference` 或 `RouteReference<T>`；ExtensionPoint 方法返回 `RouteExtensionPoint`，生成引用持有 attribute 的稳定 extension-point id，而不是 descriptor id。

## Route Kinds

- `Route`: template + ViewModel target。
- `Layout`: ViewModel target，可作为父层。
- `Index`: 有父层、无自己的 template，每个 parent/outlet 最多一个。
- `Group`: template 前缀，无 ViewModel。
- `Redirect`: template + existing target，无 ViewModel。
- `ExtensionPoint`: 稳定 id、无 template/ViewModel，不可直接导航。

## Template

支持 literal、`{id}`、`{id?}`、`{lang=en}`、末尾 `{*path}` 和多个 constraint。内置 constraint：

`bool datetime decimal double float guid int long alpha min(n) max(n) range(a,b) length(n) minlength(n) maxlength(n) regex(pattern)`。

参数名大小写不敏感；重复参数、非末尾 catch-all、括号错误、非法 regex、未知 constraint，以及不满足 constraint 的 default value 在 parse/build 时拒绝。Regex 参数允许转义括号、字符类和带逗号的量词。Catch-all constraint 对拼接后的完整剩余路径执行；default catch-all 在剩余路径为空时生效。自定义 constraint 不属于 1.0。

## Typed Parameters

```csharp
public sealed record SearchParameters
{
    public required string Term { get; init; }
    [Query] public int Page { get; init; }
    [Fragment] public string? Section { get; init; }
}
```

模板参数绑定参数类型自身或基类上的同名 public instance field，或具有 public getter 的非 indexer public instance property；派生类型同名声明遵循 C# 隐藏规则。Write-only property 和 indexer 不参与绑定；大小写不敏感的成员歧义及多个成员绑定同一路由 key 会产生编译诊断。`[Query]` 可指定 query key；`[Fragment]` 默认绑定 `fragment`。生成 binder 使用 invariant conversion 并返回只读副本。

## Generator

Generator 输出 partial 方法实现和 `AtomUI.City.Generated.GeneratedRoutingRouteManifest`。Manifest 包含 route kind、template、parent、outlet、extension point、redirect、metadata、ViewModel target、parameter names、Guard、Resolver、MatchPolicy 和 Middleware 类型。

编译期拒绝空 id/outlet、泛型或非法 map/method、手写 partial 实现、多个 route-definition attribute、非法 return type/route-kind 组合、开放泛型 ViewModel/behavior、非法 template/default、重复 id、有效完整路径上的多个无 policy fallback、missing parent/redirect、不可导航 redirect target、index/extension point 重复以及 parent/redirect cycle。拥有 attribute 的多个 partial 声明只生成一次；C# keyword 标识符会转义；输出统一使用 LF。运行时仍完整验证生成 descriptor，形成第二道防线。

Generator 要求 ViewModel/behavior 是 closed、对生成程序集可访问且有 public constructor 的 concrete class。手工构造 descriptor 的运行期验证只要求 behavior 是 concrete closed contract implementation；实例能否创建由配置的 DI/service resolver 决定。
