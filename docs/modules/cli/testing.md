# AtomUI.City.Cli Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 命令入口固定为 `atomui city`。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| 非交互和 CI 模式不得等待输入。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| JSON envelope 不能混入普通日志。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| exit code、diagnostic code、artifact schema 必须稳定。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-CLI-001 | Unit | CliCommandArchitectureTests | 断言入口名、未知命令、缺参、exit code、usage 输出和 JSON 模式隔离。 | 未知命令、缺参、非法 option 返回 InvalidArguments，不执行 handler。 | Required |
| AUC-CLI-002 | Unit | CliNewAppTests | 断言生成项目、冲突、非法名称、dry-run、JSON artifacts 和取消。 | 目标冲突、变量非法、模板缺失、render 失败不得返回成功。 | Required |
| AUC-CLI-003 | Unit | CliBuildAndTestCommandTests | 断言成功、失败、非零 exit code、取消、CI 模式和输出截断。 | dotnet 非零退出、超时、工作目录不存在返回失败 envelope。 | Required |
| AUC-CLI-004 | Unit | CliInspectDoctorPluginTests | 断言合法插件、manifest 缺失、版本非法、layout 错误和 JSON diagnostics。 | manifest 缺失、版本非法、依赖冲突、layout 错误输出 plugin diagnostics。 | Required |
| AUC-CLI-005 | Unit | CliCommandArchitectureTests | 断言 schema、纯 JSON、artifact 列表、suggested commands、retryable 语义。 | schema 缺字段、普通日志混入 JSON、颜色控制字符进入 JSON 必须测试失败。 | Required |
| AUC-CLI-006 | Unit | CliCommandArchitectureTests | 断言 CI、non-interactive、stdin unavailable、需要确认时失败。 | 缺少确认参数直接失败，不能等待 stdin。 | Required |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
