# 配置

Core 宿主基于 Microsoft.Extensions.Configuration。`IApplicationHostBuilder.Configuration`（类型 `IConfigurationManager`）用于加入配置提供程序，随后可通过 Options 模式把配置绑定到强类型对象。

## 1. 加入配置提供程序

在 `ConfigureHost` / `ConfigureServices` 之前，向 `builder.Configuration` 挂上提供程序。例如 `TodoCli` 读取环境变量（前缀 `TODOCLI_`）：

```csharp
builder.Configuration.AddEnvironmentVariables(prefix: "TODOCLI_");
```

`builder.Configuration` 是 `IConfigurationManager`，所有 `IConfigurationBuilder` 的扩展（`AddJsonFile`、`AddCommandLine`、`AddInMemoryCollection` 等）都可用。

## 2. 用 Options 模式绑定强类型配置

定义配置类，并在 `ConfigureServices` 里把某个配置节绑定给它：

```csharp
public sealed class TodoOptions
{
    public string? DefaultTitle { get; set; }
}
```

```csharp
builder.ConfigureServices(services =>
{
    services.Configure<TodoOptions>(builder.Configuration.GetSection("Todo"));
});
```

之后 `IConfiguration` / `IOptions<TodoOptions>` 即可注入。

## 3. 提供默认值：`IConfigureOptions<T>`

用 `IConfigureOptions<T>` 为尚未赋值的项填充默认值，无需依赖具体配置提供程序：

```csharp
public sealed class TodoOptionsWriter
    : IConfigureOptions<TodoOptions>
{
    public void Configure(TodoOptions options)
    {
        options.DefaultTitle ??= "untitled";
    }
}
```

`CliRunner` 通过 `IOptions<TodoOptions>` 读取配置，配合默认值实现“未提供标题时回落”：

```csharp
var title = _args.Skip(1).FirstOrDefault()
            ?? _options.Value.DefaultTitle
            ?? "untitled";
```

## 4. 配置读取顺序

结合上述机制，配置值的来源优先级（从高到低）：

1. 命令行参数 / 环境变量等由你挂载的提供程序直接提供。
2. `IConfigureOptions<T>` 设定的默认值。
3. 代码中的 `??` 兜底值。

## 5. 相关

- 宿主选项 `ApplicationHostOptions`（不是配置提供程序，而是宿主本身设置）→《宿主配置》
- Options 在 DI 中的注册 →《创建应用》
