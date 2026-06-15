# AtomUI.City.Cli Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 命令入口固定为 atomui city。
- 非交互和 CI 模式不得等待输入。
- JSON envelope 不能混入普通日志。
- exit code 稳定。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `AUCCLI0001` 表示缺少 `city` 根命令，`AUCCLI0002` 表示未知命令，`AUCCLI0003` 表示缺少子命令，`AUCCLI0004` 表示未知 option，`AUCCLI0005` 表示 value option 缺值；diagnostic `target` 和 `position` 字段进入 1.0 兼容承诺。
- `AUCCLI0101` 表示缺少 app name，`AUCCLI0102` 表示 root namespace 使用框架保留前缀，`AUCCLI0103` 表示 AOT 与 dynamic plugin 默认冲突，`AUCCLI0104` 表示 app name 不是合法 identifier，`AUCCLI0105` 表示目标文件已存在，`AUCCLI0106` 表示 new app generation 取消。
- `atomui city new app --json` 成功、dry-run、目标冲突和取消 envelope 的 `data.plan`、`data.artifacts` 字段进入 1.0 兼容承诺；目标冲突还包含 `data.conflict`。
- `AUCCLI0201` 表示 dotnet 子进程非零退出，`AUCCLI0202` 表示 dotnet 子进程取消，`AUCCLI0203` 表示工作目录不存在。
- `atomui city build/test/pack/publish --json` 的 `data.invocation`、`data.exitCode`、`data.stdout`、`data.stderr` 和 `data.durationMs` 字段进入 1.0 兼容承诺；`DotnetInvocation` 的 `workingDirectory` 和 `ciMode` 字段进入 1.0 兼容承诺。
- `--json` 模式失败时只输出 JSON envelope；非 JSON 解析失败输出 `Usage:` 文本块。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
