# 诊断

Core 提供一整套结构化诊断记录（`HostDiagnosticRecord`），宿主在构建期与运行期都会写入。应用代码也可以主动写入，便于统一记录与排障。

## 1. 诊断接口

命名空间 `AtomUI.City.Core.Diagnostics`。

```csharp
public interface IHostDiagnostics
{
    IReadOnlyList<HostDiagnosticRecord> Records { get; }
    void Write(HostDiagnosticRecord record);
    void Complete();
}
```

- `Records`：已被记录的诊断列表（容量上限由 `ApplicationHostOptions.DiagnosticsCapacity` 决定，默认 1024）。
- `Write(...)`：写入一条诊断。
- `Complete()`：标记诊断收集完成。

`HostDiagnosticRecord` 是一个 record，核心字段：

| 字段 | 说明 |
| --- | --- |
| `Code` | 诊断代码（业务自定义，如 `"TODO001"`）。 |
| `Message` | 人类可读信息。 |
| `Severity` | 严重级别：`Trace` / `Info` / `Warning` / `Error`。 |
| `ScopeId` | 可选的作用域标识。 |
| `Stage` | 可选的生命周期阶段。 |

## 2. 写入自定义诊断

`TodoCli` 的 `CliRunner` 注入 `IHostDiagnostics` 并在事件发生时写入：

```csharp
_diagnostics.Write(new HostDiagnosticRecord(
    "TODO001",
    $"Command failed: {exception.Message}",
    HostDiagnosticSeverity.Error));
```

成功路径同样记录（`Info`）：

```csharp
_diagnostics.Write(new HostDiagnosticRecord(
    "TODO002",
    $"Added todo #{item.Id}.",
    HostDiagnosticSeverity.Info));
```

> `HostDiagnosticSeverity` 的枚举值是 `Trace` / `Info` / `Warning` / `Error`（注意是 `Info`，不是 `Information`）。

## 3. 读取诊断

运行期通过 DI 解析 `IHostDiagnostics`：

```csharp
var diagnostics = host.Services.GetRequiredService<IHostDiagnostics>();
foreach (var record in diagnostics.Records)
{
    Console.WriteLine($"{record.Code}: {record.Severity} - {record.Message}");
}
```

## 4. 构建期诊断

`GetBuildDiagnostics()`（`AtomUI.City.Core.Diagnostics` 下的扩展方法）返回构建期诊断集合，可用于在启动前检查配置/模块问题：

```csharp
var builder = ApplicationHost.CreateBuilder(args);
// ... 配置 ...

var buildDiagnostics = builder.GetBuildDiagnostics();
if (buildDiagnostics.Records.Any(r => r.Severity == HostDiagnosticSeverity.Error))
{
    // 构建已失败，不要继续
}
```

## 5. 记录与规格

- 诊断容量：`DiagnosticsCapacity`（见《宿主配置》）。
- 相关宿主内置诊断 ID（如 `HostBuilt`、`HostStartFailed` 等）定义于 `HostDiagnosticIds`。
- 更多内部诊断契约见 `docs/modules/core/diagnostics.md`。
