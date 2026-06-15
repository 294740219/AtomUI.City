# AtomUI.City.Cli API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Command Entry | Program, CliApplication, CliCommandLine | `atomui city` 命令入口和解析。 | 未知命令、非法参数稳定失败。 |
| Envelope | CliEnvelope, CliDiagnostic, CliExitCodes | 机器可读输出和诊断。 | `--json` 只能输出 JSON envelope。 |
| Process Invocation | DotnetInvocation, ProcessRunner | 调用 dotnet 子进程。 | 保留 exit code 和 stdout/stderr 摘要。 |
| Environment | CliExecutionEnvironment | CI、非交互和工作目录。 | 非交互不等待输入。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| CliApplication.RunAsync | 执行命令。 | argv、environment、stdout/stderr。 | exit code。 | 解析失败、handler 失败映射 CliExitCodes；缺少 `city`、缺少子命令、未知 command、未知 option 和 option 缺值都返回 ArgumentError。 | 必须观察 token。 | 单进程单次调用；handler 内部并发隔离。 |
| CliCommandLine.Parse | 解析命令行。 | argv 不得为 null。 | CliCommandLine 和 parse diagnostics。 | 未知 option、缺参返回 parse diagnostic，不执行 handler。 | 纯 CPU，无 token。 | 无共享状态。 |
| `atomui city new app` handler | 生成应用模板。 | AppName、namespace、target framework、output、dry-run、AOT/plugin flags。 | exit code 和 CliEnvelope。 | `AUCCLI0101` 到 `AUCCLI0106` 覆盖缺参、保留 namespace、冲突变量、非法 app name、目标冲突和取消；目标冲突不得覆盖已有文件。 | 渲染前和每个文件写入前观察 token；取消输出失败 envelope。 | 先 plan 后 render；dry-run 可重复调用且不写文件。 |
| CliEnvelope JSON/Text 输出 | 输出机器可读 envelope 或文本摘要。 | envelope、TextWriter。 | 写入完成。 | JSON 模式只写 JSON envelope；文本失败输出稳定 usage。 | 写入前观察 token。 | JSON 输出不得混入普通日志。 |
| ProcessRunner.RunAsync | 运行 dotnet 或其他子进程。 | DotnetInvocation。 | process result。 | 非零 exit code 保留为 command failure。 | 取消必须终止子进程或停止等待。 | stdout/stderr 缓冲必须有上限。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `CliApplication` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CliDiagnostic` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CliEnvelope` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CliExecutionEnvironment` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `CliExitCodes` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DotnetInvocation` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
