# 依赖注入

Core 的依赖注入（DI）构建在 Microsoft.Extensions.DependencyInjection 之上，并通过 `AtomUI.City.Generators` 在**编译期**扫描服务声明，自动生成注册代码，减少手写样板。

## 1. 生成器注册属性

命名空间 `AtomUI.City.Core.DependencyInjection`。

### `[Service(ServiceLifetime.X)]`

把类注册为服务，生命周期由 `ServiceLifetime` 决定（`Singleton` / `Scoped` / `Transient`）。需要 `using Microsoft.Extensions.DependencyInjection;` 才能使用 `ServiceLifetime`。

```csharp
using AtomUI.City.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[Service(ServiceLifetime.Singleton)]
[ExposeServices(typeof(ITodoStore))]
public sealed class TodoStore : ITodoStore
{
    // ...
}
```

`[Service]` 还支持可选属性：`Replace`（替换已有注册）、`TryAdd`（若已存在则跳过）、`Key`（注册键）。

### `[ScopedService(params Type[] serviceTypes)]`

便捷注册为 `Scoped`，并一次性暴露给定服务类型：

```csharp
[ScopedService(typeof(IFoo), typeof(IBar))]
public sealed class FooBar : IFoo, IBar;
```

### `[ExposeServices(typeof(I))]`

**把具体类额外暴露为指定接口**。上面 `TodoStore` 因为 `[ExposeServices(typeof(ITodoStore))]`，才能通过 `ITodoStore` 解析：

```csharp
var store = host.Services.GetRequiredService<ITodoStore>();      // OK
var store2 = host.Services.GetRequiredService<TodoStore>();     // 也 OK
```

### 标记接口（Marker Interfaces）

还有三个**空标记接口**，作为“快捷注册”方式：

- `ISingletonDependency`
- `IScopedDependency`
- `ITransientDependency`

```csharp
public sealed class TraceSink : ITransientDependency
{
    // ...
}
```

> **重要行为**：标记接口注册的**服务类型是具体类本身，而不是你自定义的接口**。也就是说 `TraceSink` 可以通过 `GetRequiredService<TraceSink>()` 解析，但**不会**自动暴露某个自定义接口。若要按自定义接口解析，请使用 `[ExposeServices(typeof(IMyInterface))]` 或 `[Service]` + `[ExposeServices]`。

`TodoCli` 里同时演示了两种风格：

- `TodoStore`：属性风格（`[Service]` + `[ExposeServices]`），可暴露接口 `ITodoStore`。
- `TraceSink`：标记风格（`ITransientDependency`），按具体类解析。

## 2. 注册归属：`[ServiceRegistrationOwner]`

在**应用模块**上加上 `[ServiceRegistrationOwner]`，标明该程序集的生成器注册以此模块为归属。`TodoCli` 的 `CliModule` 同时带有 `[ApplicationModule]` 与 `[ServiceRegistrationOwner]`：

```csharp
[ApplicationModule]
[ServiceRegistrationOwner]
public sealed class CliModule : ModuleBase;
```

## 3. 通过模块的 `ConfigureServices` 注册

非生成器路径下，也可以在模块里用普通 `IServiceCollection` 注册：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.AddSingleton<IMyService, MyService>();
}
```

生成器注册与模块 `ConfigureServices` 注册会合并在同一个容器中。

## 4. 解析服务的时机

模块初始化阶段，注入的解析对象已经可用：

- 在模块的 `ConfigureServices` 里**不能**解析服务（此时容器尚未建好），只应**注册**。
- 在 `OnApplicationInitialization`（及其它初始化钩子）里，可通过 `context.ServiceProvider` 解析并初始化服务。
- 在宿主启动完成后，可通过 `host.Services.GetRequiredService<T>()` 解析根级服务。

## 5. 参数注册

CLI 里常见的做法是把 `string[] args` 注册为单例，供命令执行器解析（详见《创建应用》）：

```csharp
builder.ConfigureServices(services =>
{
    services.AddSingleton(args);
});
```

## 6. 参考

- 模块与 `ConfigureServices`：`docs/guides/core/create-module.md`
- 创建应用宿主：`docs/guides/core/create-application.md`
- 生成器内部设计：`docs/modules/generators/overview.md`
