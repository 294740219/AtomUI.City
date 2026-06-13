# AtomUI.City.Presentation Activation Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Presentation` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Activation Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- Presentation 负责 ViewModel -> View -> Outlet -> VisualTree。
- VisualTree 变化必须通过生命周期事件或绑定反馈回 ViewModel/State。
- View 创建和提交必须在 UI dispatcher 上执行。

## Public Contract

- 只允许通过 `AtomUI.City.Presentation` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-PRESENTATION-001 | UI Dispatcher | AvaloniaUiDispatcherTests |
| AUC-PRESENTATION-002 | View Locator | ViewLocatorTests |
| AUC-PRESENTATION-003 | View Binding | ViewBindingTests |
| AUC-PRESENTATION-004 | Route Outlet | RouteOutletTests |
| AUC-PRESENTATION-005 | Presentation Runtime | PresentationRuntimeTests |
| AUC-PRESENTATION-006 | Localization Bridge | PresentationLocalizationBridgeTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.PluginSystem 运行时直接依赖插件实现类型` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Presentation Activation 集成设计

适用范围：Visual lifecycle、ActivationScope、attached/detached、close intent 和 ViewModel 激活边界

### 1. 定位

Mvvm Activation 是 ViewModel 生命周期。Presentation 负责把 visual lifecycle 接入 Activation。

Visual lifecycle 和 ViewModel active 状态不能混为一谈。

| 概念 | 来源 | 含义 |
|---|---|---|
| ActivationScope | Routing / Mvvm | ViewModel 逻辑上进入当前路由或激活上下文。 |
| VisualAttachmentState | Presentation / AtomUI/Avalonia | View 当前是否挂在 visual tree 或处于可见生命周期。 |

### 2. 导航提交阶段

```text
Prepare:
Create provisional RouteScope / ActivationScope
Create ViewModel
Resolve ViewDescriptor
Create View
Bind View and ViewModel

Commit:
Apply Outlet commit plan
Attach / replace / detach views
Update AtomUI/Avalonia VisualTree

Activate:
Mark ActivationScope running
Activate ViewModel
Attach visual lifecycle adapter
Update NavigationSnapshot / Journal
```

ActivationScope 在 binding 前可用，但只有 Outlet commit 成功后，ViewModel 才进入 active 状态。

### 3. VisualTree 反馈

VisualTree 变化必须通过 Presentation 归一化后反馈。Routing、Mvvm、Core 不直接订阅 AtomUI/Avalonia 原始 visual tree 事件。

反馈分为：

- Outlet commit 反馈。
- Visual lifecycle 反馈。
- Leave / close intent。
- Diagnostics。

### 4. Close Intent

关闭类事件必须表达为意图，而不是直接释放对象。

```text
Window Closing / View close gesture
-> Presentation captures close intent
-> Routing LeaveGuard / Mvvm Interaction confirmation
-> if allowed: deactivate route or window
-> Presentation detaches VisualTree
```

### 5. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| commit 后激活 | Unit | commit 成功后 ActivationScope running。 |
| commit 失败 | Unit | provisional Scope 被释放。 |
| attached feedback | Unit | visual state 反馈到 activation adapter。 |
| detached feedback | Unit | visual state 更新但不直接停用 ViewModel。 |
| close intent | Unit | 转为 leave request 或 interaction。 |
