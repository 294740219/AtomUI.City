# 创建模块

模块（Module）是 Core 中组织功能与生命周期钩子的单元。一个应用通常包含一个或多个模块。

## 1. 最小模块

`[ApplicationModule]` 标记一个模块为应用模块；`[ServiceRegistrationOwner]` 标明该程序集中的服务注册以此模块为归属（生成器据此组织注册）：

```csharp
using AtomUI.City.Core.DependencyInjection;
using AtomUI.City.Core.Modularity;

namespace TodoCli;

[ApplicationModule]
[ServiceRegistrationOwner]
public sealed class CliModule : ModuleBase;
```

`TodoCli` 用一个极简的 `CliModule` 作为服务与生成器注册的归属；真正的逻辑都在被注入的服务里。

## 2. 依赖其它模块

用 `[DependsOn(typeof(OtherModule))]` 声明依赖。依赖项会先于当前模块初始化：

```csharp
[ApplicationModule]
[DependsOn(typeof(FoundationModule))]
public sealed class MyModule : ModuleBase;
```

> `[DependsOn]` 属于已冻结能力（AUC-CORE 范围内）。生成器与宿主会据此做模块排序与验证。

## 3. 模块钩子

模块继承 `ModuleBase`，可按需覆写下列钩子。这些钩子在宿主生命周期中按固定顺序执行：

**服务配置阶段（同步）：**
- `PreConfigureServices(ServiceConfigurationContext)`——最先，可对前面模块的注册做 `TryAdd` / 预置。
- `ConfigureServices(ServiceConfigurationContext)`——注册本模块服务。
- `PostConfigureServices(ServiceConfigurationContext)`——最后，可对注册做覆盖/重排。

**贡献（Contribution）阶段：**
- `ConfigureContributions(ContributionConfigurationContext)` / `ConfigureContributionsAsync(...)`。

**初始化阶段（异步，按 `OnPre → On → OnPost` 顺序）：**
- `OnPreApplicationInitialization(ApplicationInitializationContext)`
- `OnApplicationInitialization(ApplicationInitializationContext)`
- `OnPostApplicationInitialization(ApplicationInitializationContext)`

**关闭阶段：**
- `OnApplicationShutdown(ApplicationShutdownContext)`

> 每个操作都有对应的 `Async` 重载并接受 `CancellationToken`。多数场景在同步版本里完成即可。

示例——在模块初始化时解析服务执行初始化（覆写同步版本最省事）：

```csharp
[ApplicationModule]
public sealed class MyModule : ModuleBase
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // context.Services 即 IServiceCollection，可注册服务
        context.Services.AddSingleton<IMyService, MyService>();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var service = context.ServiceProvider.GetRequiredService<IMyService>();
        service.Initialize();
    }
}
```

若需要异步初始化，覆写 `OnApplicationInitializationAsync(ApplicationInitializationContext, CancellationToken)` —— **请保持 `ValueTask` 返回类型**（`ModuleBase` 的签名是 `ValueTask`），并在内部 `await`：

```csharp
public override async ValueTask OnApplicationInitializationAsync(
    ApplicationInitializationContext context,
    CancellationToken cancellationToken = default)
{
    var service = context.ServiceProvider.GetRequiredService<IMyService>();
    await service.InitializeAsync(cancellationToken);
}
```

## 4. 参考

- 服务注册属性与生成器：`docs/guides/core/dependency-injection.md`
- 宿主生命周期：`docs/guides/core/create-application.md`
- 模块内部设计：`docs/modules/core/modularity.md`
