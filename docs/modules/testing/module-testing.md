# AtomUI.City.Testing Module Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Testing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Module Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

## Public Contract

- 只允许通过 `AtomUI.City.Testing` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- Owner 必须明确：Host、Module、Plugin、Route、Operation、Connection、View 或 Test scope。
- 释放必须幂等；释放后 mutating API 必须失败或返回声明的 Result。
- Cancellation 必须在进入外部调用、用户 handler、插件代码、IO、dispatcher work 前后观察。
- 插件来源对象必须可撤销，不能泄漏到 Host 根单例。

## 失败行为

- 输入无效：使用标准参数异常或模块 Result。
- 生命周期状态非法：返回失败 Result、模块异常或稳定诊断。
- 依赖缺失：阻止当前功能启用，不影响无关功能。
- 插件卸载中：拒绝创建新贡献，并撤销已有贡献。
- 释放失败：记录诊断并继续释放其他资源。

## 测试要求

| Feature ID | 相关能力 | 测试文件 |
| --- | --- | --- |
| AUC-TESTING-001 | Test Host | TestHostTests |
| AUC-TESTING-002 | Fake Dispatcher | FakeUiDispatcherTests |
| AUC-TESTING-003 | Deterministic Scheduler | SharedTestUtilitiesTests |
| AUC-TESTING-004 | Module Test Host | ModuleTestHostTests |
| AUC-TESTING-005 | Plugin Test Host | PluginTestHostTests |
| AUC-TESTING-006 | Routing Test Host | RoutingTestHostTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `生产运行时反向依赖 AtomUI.City.Testing` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## Module 测试设计

适用范围：模块定义、模块依赖、模块图、配置阶段、服务注册阶段、模块生命周期和插件模块

### 1. 目标

Module 测试用于验证模块作为应用组成单元的行为。模块测试不能通过真实应用启动掩盖模块依赖、顺序和贡献问题。

### 2. ModuleTestHost

`ModuleTestHost` 提供：

- 测试模块注册。
- 模块图构建。
- 拓扑排序断言。
- 模块生命周期阶段驱动。
- 配置阶段断言。
- 服务注册阶段断言。
- Contribution 记录。

### 3. 依赖图测试

必须覆盖：

- 显式依赖。
- 默认模块 Id。
- 重复模块。
- 缺失依赖。
- 循环依赖。
- 拓扑排序。
- 插件模块依赖。

### 4. 配置阶段测试

必须覆盖：

- PreConfigure。
- Configure。
- PostConfigure。
- Options validation。
- 插件配置隔离。
- 配置失败错误策略。

### 5. 服务注册测试

必须覆盖：

- 服务注册进入正确 ServiceCollection。
- 模块服务不提前构建 ServiceProvider。
- 插件模块不能修改 Host Root ServiceProvider。
- 自动服务注册清单生效。
- AOT 模式拒绝运行时扫描。

### 6. 生命周期测试

必须覆盖：

- 模块初始化顺序。
- 模块启动顺序。
- 模块停止顺序。
- 停止反向顺序。
- 失败回滚。
- 诊断记录。

### 7. 测试要求

每个模块功能点必须有单元测试。模块组合行为使用 ModuleTestHost 做 framework integration test。
