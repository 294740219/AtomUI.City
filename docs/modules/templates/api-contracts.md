# AtomUI.City.Templates API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Options | ApplicationTemplateOptions | 模板输入和命名规则。 | 非法名称不写文件。 |
| Planning | ApplicationTemplateRenderer, TemplatePlan | 生成可 review 的文件变更计划。 | plan 先于写入。 |
| Rendering | TemplateChange, TemplateRenderResult | 应用模板文件变更。 | 冲突、路径逃逸、写入失败有稳定 result。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| ApplicationTemplateRenderer.CreatePlan | 生成模板变更计划。 | options、target directory。 | TemplatePlan。 | 非法名称、目标逃逸、冲突标记失败。 | 纯 CPU/文件枚举可观察 token。 | 不写文件，可重复调用。 |
| ApplicationTemplateRenderer.RenderAsync | 执行模板写入。 | TemplatePlan 或 options。 | TemplateRenderResult。 | 写入失败返回 diagnostics，并记录已写入文件。 | 取消后停止后续写入并返回 partial result。 | 同一目标目录并发 render 必须拒绝或文件锁保护。 |
| TemplatePlan.Validate | 校验计划。 | plan entries。 | validation result。 | 重复路径、路径逃逸、非法 overwrite 失败。 | 纯 CPU，无 token。 | plan 不可变。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `ApplicationTemplateOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationTemplateRenderer` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TemplateChange` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TemplatePlan` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TemplateRenderResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TemplateDiagnostic` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

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
