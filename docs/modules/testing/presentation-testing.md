# AtomUI.City.Testing Presentation Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Testing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Presentation Testing` 相关实现决策，不重新定义模块边界。

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

## Presentation 测试设计

适用范围：Presentation fake runtime、ViewLocator、ViewFactory、Outlet commit、UI Dispatcher、visual lifecycle 和 AtomUI/Avalonia 平台集成

### 1. 目标

Presentation 测试分为 fake runtime 测试和真实平台集成测试。默认测试不启动真实 AtomUI/Avalonia UI。

### 2. PresentationTestRuntime

Fake runtime 提供：

- fake ViewLocator。
- fake ViewFactory。
- fake route outlet。
- fake visual tree。
- fake UI dispatcher。
- fake interaction handler。
- fake resource dictionary。
- visual lifecycle feedback driver。

### 3. 单元测试范围

必须覆盖：

- ViewModel 到 View 映射。
- ViewLocator success/failure。
- ViewFactory create/dispose。
- Outlet commit。
- Commit failure rollback。
- UI dispatcher 投递。
- Visual attached/detached。
- Loaded/unloaded feedback。
- Resource lease revoke。
- 插件 View/Resource 撤销。

### 4. MVVM 集成

必须覆盖：

- ViewModel 创建后未 commit 不 active。
- commit 成功后 activation。
- visual detached 后 deactivation。
- Interaction handler 绑定和释放。
- Binding error diagnostics。

### 5. 平台集成测试

真实 AtomUI/Avalonia 测试只覆盖 fake runtime 无法证明的行为：

- 真实 UI Dispatcher。
- 真实 ViewLocator。
- 真实 binding。
- 真实 Outlet commit 到 visual tree。
- ResourceDictionary 刷新。
- Window lifecycle。
- visual attach/detach。

平台集成测试必须独立分类，不作为普通单元测试替代。

### 6. 测试要求

Presentation 的每个 public contract 和每个运行时桥接点必须有单元测试。真实 UI 行为使用平台集成测试补充。
