# AtomUI.City.Mvvm Interactions 合同

## 适用范围

本专题属于 `AtomUI.City.Mvvm` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Interactions` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.Mvvm` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-MVVM-001 | ViewModel Base | ViewModelBaseTests |
| AUC-MVVM-002 | Activation and Deactivation | ActivationScopeTests; DeactivationTests; ViewModelBaseTests |
| AUC-MVVM-003 | Commands | CommandTests |
| AUC-MVVM-004 | Interactions | InteractionTests |
| AUC-MVVM-005 | Validation | ValidationScopeTests |
| AUC-MVVM-006 | Operation Scope | CommandTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Mvvm Interactions 设计

适用范围：ViewModel 到 Presentation 的交互请求、Interaction handler、结果模型、ActivationScope 绑定和测试支持。

### 1. 定位

Interaction 用来让 ViewModel 发起 UI 交互请求，同时避免 ViewModel 直接依赖窗口、Dialog、MessageBox 或 AtomUI 控件。

Interaction 是 ViewModel 和 Presentation 之间的受控桥接。

### 2. 非目标

Interaction 不负责：

- 具体 Dialog 控件实现。
- 窗口管理。
- 路由跳转。
- 通知系统的 UI 呈现。
- 文件系统权限实现。

这些由 Presentation、Routing 或应用层实现。

### 3. 模型

建议提供泛型 Interaction Request：

```text
ViewModel
-> Interaction<TRequest, TResult>
-> Presentation handler
-> Result back to ViewModel
```

Interaction 适合：

- 确认。
- 输入。
- 文件选择。
- 通知。
- 需要 UI 承接的用户交互。

### 4. Handler 生命周期

Interaction handler 必须绑定 ActivationScope。

```text
ActivationScope
-> Register interaction handler
-> Handle interaction requests
-> Dispose handler on deactivation
```

ViewModel 停用时，未完成 interaction 应取消或返回明确的 canceled result。

### 5. 结果模型

Interaction 结果必须区分：

| 结果 | 含义 |
|---|---|
| Completed | 交互完成并返回结果。 |
| Canceled | 用户或生命周期取消。 |
| Failed | handler 执行失败。 |
| NotHandled | 没有可用 handler。 |

Interaction handler 缺失不应该导致应用崩溃。每次请求的 `InteractionContext` 必须提供 request id、request type、handler type 和 ActivationScope id 作为诊断上下文。

### 6. 插件边界

插件 ViewModel 可以发起 Interaction。

插件 Interaction 必须满足：

- handler 绑定插件产生的 ActivationScope。
- 请求携带 PluginId、ModuleId、ContributionId。
- 插件停用时取消未完成请求。
- 插件不能直接持有 Host UI 对象。

### 7. Presentation 集成

Presentation 负责把 Interaction Request 映射到具体 UI。

Mvvm 只定义：

- request。
- result。
- handler contract。
- lifecycle binding。
- diagnostics。

Presentation 可以根据平台实现 Dialog、Toast、FilePicker、Window 等交互。

### 8. 错误策略

| 场景 | 默认处理 |
|---|---|
| handler 缺失 | 返回 NotHandled，记录诊断。 |
| handler 抛异常 | 返回 Failed，记录诊断。 |
| ActivationScope 停用 | 返回 Canceled，且不得提交之后完成的 handler result。 |
| Plugin 停用 | 返回 Canceled。 |

### 9. AOT / Source Generator

Generator/Analyzer 可负责：

- 生成 interaction descriptor。
- 诊断 interaction id 重复。
- 诊断 request/result 类型不稳定。
- 诊断 handler 未绑定 ActivationScope。
- 生成 manifest，供 Presentation 和 Testing 使用。

### 10. 测试策略

Testing 包应支持：

- 捕获 Interaction request。
- 注入 fake handler。
- 断言 Completed/Canceled/Failed/NotHandled。
- 断言 ActivationScope 释放时取消未完成请求。
- 断言插件停用时取消请求。
- 断言诊断记录。
