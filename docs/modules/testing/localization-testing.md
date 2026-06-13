# AtomUI.City.Testing Localization Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Testing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Localization Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 语言包按当前 culture 懒加载。
- assembly 语言包必须支持运行时加载和撤销。
- 缺失 key 必须输出诊断并走 fallback。
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

## Localization 测试设计

适用范围：culture state、资源查找、fallback、懒加载、语言包 assembly、locpack、UI refresh 和插件本地化

### 1. 目标

Localization 测试必须证明语言切换、资源查找、懒加载和 UI refresh 可预测、可撤销、可诊断。

### 2. LocalizationTestKit

Testing 提供：

- fake culture state provider。
- fake language package provider。
- fake assembly package provider。
- fake locpack provider。
- resource lookup recorder。
- fallback assertion。
- culture switch driver。
- UI refresh fake bridge。
- resource leak assertion helper。

### 3. 单元测试范围

必须覆盖：

- current culture 读取。
- culture switch。
- resource lookup。
- missing key。
- fallback chain。
- culture package lazy load。
- language package assembly load。
- locpack load。
- resource unload。
- diagnostics。

### 4. UI refresh 测试

Fake bridge 覆盖：

- culture change 通知。
- ResourceDictionary 更新请求。
- ViewModel 错误文本刷新。
- command text refresh。
- route metadata refresh。

真实 ResourceDictionary 行为放平台集成测试。

### 5. 插件本地化测试

必须覆盖：

- 插件语言包注册。
- 插件语言包懒加载。
- 插件停用后资源撤销。
- 插件卸载后 language package unload。
- 插件缺失资源 fallback。

### 6. 测试要求

Localization Core 测试不依赖真实 AtomUI/Avalonia。
