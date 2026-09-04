# AtomUI.City.EventBus Contracts 合同

## 适用范围

本专题属于 `AtomUI.City.EventBus` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Contracts` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.EventBus` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-EVENTBUS-003 | Contract Identity & Registry | EventContractRegistryTests |
| AUC-EVENTBUS-008 | Generated Event Catalog & NativeAOT | EventBusGeneratorTests; EventBusNativeAotProcessTests |
| AUC-EVENTBUS-009 | Plugin Event Planes | EventBusPluginContractTests; EventBusPluginLifecycleTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.EventBus Event Contracts 设计

适用范围：事件契约身份、共享 Contract 程序集、AssemblyLoadContext、插件跨边界事件、版本兼容、对象图约束、manifest 和卸载设计。

### 1. 定位

Event Contract 定义 EventBus 中可以发布什么数据，以及 Host、静态模块和运行时插件如何对同一个事件类型形成一致认识。

跨插件边界时，Contract 设计直接决定：

- CLR 类型身份是否一致。
- 插件能否匹配 Host 发布的事件。
- Contract 版本是否兼容。
- Host 是否会意外持有插件私有类型。
- 插件的 `AssemblyLoadContext` 能否卸载。

因此，Event Contract 不是普通 DTO 约定，而是 Host 和插件之间的稳定运行时边界。

### 2. CLR 类型身份

.NET 类型身份不仅由 namespace 和 type name 决定，还包括定义它的 Assembly 以及加载该 Assembly 的 `AssemblyLoadContext`。

即使两个类型具有完全相同的名称：

```text
MyApplication.Contracts.WorkspaceChangedEvent
```

如果它们分别由 Default AssemblyLoadContext 和 Plugin AssemblyLoadContext 加载，运行时仍然认为它们是不同类型：

```text
Default ALC::MyApplication.Contracts.WorkspaceChangedEvent
!=
Plugin ALC::MyApplication.Contracts.WorkspaceChangedEvent
```

直接后果：

- Host 发布的事件无法匹配插件订阅。
- 强制类型转换失败。
- 泛型 handler 无法调用。
- EventBus 被迫退化为反射或序列化桥接。
- Host 缓存插件 ALC 中的 `Type` 时会阻止插件卸载。
- 不同插件携带不同 Contract 版本时会产生类型冲突。

### 3. 跨边界 Contract 规则

任何需要在 Host、静态模块或多个插件之间发布和订阅的事件，其事件类型及完整对象图必须：

- 来自 Host 注册的共享 Contract 程序集。
- 由 Default AssemblyLoadContext 唯一加载。
- 在 Host Contract Registry 中有唯一描述。
- 使用稳定 EventContractId。
- 满足版本兼容规则。
- 不包含插件私有类型。

插件私有事件类型只能在插件私有 EventBus 平面内使用，不能进入 Host 共享事件总线。

### 4. 共享 Contract 程序集

共享 Contract 程序集是由 Host 管理、由多个运行边界共同引用、由 Default AssemblyLoadContext 唯一加载的稳定契约程序集。

推荐分类：

| 程序集类别 | 职责 |
|---|---|
| `AtomUI.City.EventBus.Abstractions` | EventBus 基础接口、上下文和稳定元数据类型。 |
| Application contracts | 当前应用允许模块和插件共同使用的事件契约。 |
| Extension contracts | 某个扩展点或插件生态共同使用的事件契约。 |

框架不要求所有应用事件放进一个巨大程序集。应用可以按稳定边界拆分多个 Contract 包，但每个包都必须由 Host 注册和统一加载。

业务事件不能放进 `AtomUI.City.EventBus` 框架包。框架只提供事件机制和基础 contract。

### 5. 推荐加载结构

```text
Default AssemblyLoadContext
├── AtomUI.City.EventBus.Abstractions
├── MyApplication.Contracts
└── DocumentExtension.Contracts

Plugin A AssemblyLoadContext
├── PluginA.dll
└── PluginA.Private.dll

Plugin B AssemblyLoadContext
├── PluginB.dll
└── PluginB.Private.dll
```

插件可以在构建时引用 `MyApplication.Contracts` 或 `DocumentExtension.Contracts`，但运行时不能在自己的 ALC 中再次加载这些 Assembly。

### 6. Contract 解析流程

PluginSystem 加载插件依赖时必须先查询 Host Contract Registry：

```text
Plugin requests an assembly
-> Check Host shared contract registry
-> Validate assembly identity and version
-> Return assembly already loaded by Default ALC
-> Do not load plugin-local contract copy
```

如果插件包携带共享 Contract 的本地副本：

- Plugin loader 必须忽略该副本。
- 必须验证其编译目标版本与 Host 提供版本兼容。
- 不允许插件本地副本覆盖 Host Contract。
- 不兼容时在 PluginVerify 或 PluginLoad 阶段拒绝插件。

不能等到第一次事件发布时才发现类型不匹配。

### 7. Host Contract Registry

Host 维护共享契约注册表。它可以与全局 `IHostContractRegistry` 集成，并由 EventBus 暴露专用读取模型。

每个 Contract Assembly descriptor 至少包含：

- Assembly name。
- Assembly version。
- Public key token。
- Contract package id。
- Contract version。
- 兼容版本范围。
- 来源。
- 是否允许插件引用。
- 允许的 capability。
- 包含的 EventContract descriptor。

每个 EventContract descriptor 至少包含：

- EventContractId。
- CLR type。
- Contract version。
- Declaring contract assembly。
- Schema fingerprint。
- Publish capability。
- Subscribe capability。
- Channel definitions。
- Compatibility metadata。
- Diagnostic metadata。

运行时发布热路径使用预构建 descriptor，不反复反射读取 Assembly 和 Attribute。

Application Plane 1.0 的 Shared Registry public surface 还必须提供 `IsFrozen`、只读 `Descriptors` 快照、`Freeze()`，以及按 `EventContractId` 和精确 `Type` 的 `TryGet`。`Register` 只在 MutableDuringConfiguration 阶段成功；`Freeze` 与 `Register` 竞争时只允许“完整进入快照”或“明确拒绝”两种结果。

DI 生产路径必须在构造 EventBus 前执行一次 Registry 准备事务：将 `AddEventContract<TEvent>` 收集的 descriptor 汇入尚未冻结的 Registry，然后冻结并逐项核验 ContractId 与精确 Type 映射。调用方替换 `IEventContractRegistry` 不能绕过该事务；已经冻结却缺失已收集 descriptor 的自定义 Registry 必须阻止 EventBus 创建，不能静默丢弃配置。

`GetOrCreate<TEvent>` 只作为当前手工构造 `InMemoryEventContractRegistry` 时的应用内部事件过渡入口：仅在未冻结状态、且事件类型来自 Default AssemblyLoadContext 时允许创建默认 descriptor。DI 生产路径必须在 EventBus 可解析前冻结 Registry；冻结后该方法对未知类型执行 required lookup 并失败，不能在发布热路径修改身份表。插件或 collectible ALC 类型在任何状态下都不能通过该入口进入 Shared Plane。

Contract version、schema fingerprint、兼容版本范围和完整对象图由 `AUC-EVENTBUS-008` 的 generated descriptor/analyzer 提供；`AUC-EVENTBUS-003` 当前只负责保存和验证 ContractId、精确 Type、Assembly、AssemblyLoadContext 和 plane 身份边界，不能为了提前填充这些字段而在运行时反射扫描对象图。Plugin Private Registry 的创建、动态快照和释放由 `AUC-EVENTBUS-009` 落实。

### 8. EventContractId

EventContractId 是事件跨版本和跨加载边界的稳定身份。

示例：

```text
workspace.changed.v1
document.saved.v1
plugin.state-changed.v1
```

规则：

- Id 在 Host 共享事件平面内唯一。
- Id 使用稳定、可读、与 CLR 类型名解耦的形式。
- Host shared registry 必须拒绝重复 contract id、重复 event type 和 plugin-private descriptor，不能通过重复注册静默覆盖已有映射。
- Id 一旦公开不能改变语义。
- 破坏性变更创建新 Id 或新 major version。
- default `EventContractId` 不是已创建 id，Shared 和 PluginPrivate descriptor 都必须拒绝。
- 未显式声明的内部事件可以由 source generator 使用类型全名生成默认 Id。
- 插件共享事件应显式声明 Id。

Contract Id 不用于把任意无类型 payload 转换为动态消息。运行时仍然保留强类型映射。

### 9. Contract 类型设计

跨边界事件类型应该是简单、不可变的数据契约。

推荐：

- `sealed record`。
- `readonly record struct`。
- 只读属性。
- 构造时完成初始化。
- 明确 nullability。
- 简单、稳定的数据结构。

Shared Contract 1.0 使用封闭白名单，只允许：

- `bool`、整数、浮点数、`decimal`、`char`、`string`。
- `Guid`、`DateTime`、`DateTimeOffset`、`DateOnly`、`TimeOnly`、`TimeSpan`。
- Contract Assembly 自己定义的 enum。
- Contract Assembly 自己定义的 sealed、无自定义基类、非 abstract 不可变 class，或 readonly struct；公开属性只能 get/init，实例字段必须 readonly 或是 get/init 自动属性的 backing field。
- `Nullable<T>`、`ImmutableArray<T>`、`KeyValuePair<TKey,TValue>`，且全部泛型参数递归满足本白名单。

数组、集合接口和任意其他外部程序集类型不在 1.0 白名单中。需要多值数据时使用 `ImmutableArray<T>`；需要字典语义时使用 `ImmutableArray<KeyValuePair<string,T>>`，避免接口实际实例或自定义 comparer 持有插件类型。

不推荐使用继承层次表达事件多态。默认只进行精确 contract 匹配，避免运行时扫描继承树和产生隐式订阅范围。

### 10. 完整对象图约束

仅仅让事件根类型位于共享 Contract Assembly 还不够。事件的完整对象图也不能包含插件私有对象。

禁止携带：

- AtomUI/Avalonia View、Control、Window。
- ViewModel。
- `IServiceProvider`。
- Delegate。
- `Task`、`CancellationTokenSource`。
- Stream 和开放资源句柄。
- 插件服务实例。
- 插件私有异常。
- 插件私有 `Type` 或反射对象。
- 可变全局集合。
- 无约束 `object` 扩展数据。
- `dynamic`、interface、abstract class、数组、开放或用户自定义泛型。
- 任意未进入封闭白名单的外部 BCL/第三方类型。

以下设计不允许作为共享 Contract：

```csharp
public sealed record DataChangedEvent(object Data);
```

即使 `DataChangedEvent` 位于共享 Assembly，`Data` 仍可能引用插件私有实例，从而把插件类型泄漏给 Host 和其他订阅者。

如果确实需要扩展字段，应使用 Contract Assembly 明确定义的 sealed value object，或 `ImmutableArray<KeyValuePair<string,string>>`。第一版不接受任意 `object` property bag、只读集合接口或仅靠运行时约定的可序列化 schema。

生成器必须在编译期递归验证上述完整对象图并生成安全证明；运行时发布热路径不得使用反射重新扫描或通用深复制。手工 `AddEventContract<TEvent>` 仍可用于 Application Plane 内部事件，但其 descriptor 没有 generated object-graph proof，不得授予插件 Shared Publish/Subscribe capability。

Generated handler 也必须在编译期证明其 `IEventHandler<TEvent>` 指向可达的 generated Shared contract。当前 compilation 以有效 contract candidate 为准；引用程序集以 `EventContractAttribute`、`GeneratedEventManifestAttribute`、Core `GeneratedServiceManifestAttribute` 三者共同证明，且两个 manifest 必须指向同一个 registrar。Host 随后基于实际选中的 Module contribution 再做一次 handler/contract 闭包验证；这允许 Module 合法订阅另一 Module 的 Shared contract，同时拒绝只选择 handler owner、却遗漏 contract owner 的应用组合。

### 11. Shared Plane 与 Private Plane

EventBus 存在两个逻辑平面：

| 平面 | Contract 来源 | 可见范围 | 生命周期 |
|---|---|---|---|
| Shared Contract Plane | Host 注册的共享 Contract Assembly | Host、静态模块、授权插件 | ApplicationScope |
| Plugin Private Plane | 插件自己的 Assembly | 当前插件内部 | 插件生命周期 |

共享事件：

```text
Host or module publishes
-> Shared Contract Plane
-> Host/module/authorized plugin handlers
```

插件私有事件：

```text
Plugin publishes private event
-> Plugin Private Plane
-> Same plugin handlers only
```

插件私有事件不能因为 namespace 看起来公共就进入 Shared Plane。判断依据是加载期生成并注册的 EventContract descriptor。PluginPrivate descriptor 仍必须持有已创建的 `EventContractId`，不能使用 default id。

`EventContractDescriptor.PluginPrivate<TEvent>` 只接受 collectible、non-default AssemblyLoadContext 中定义的类型。Default ALC 类型不能通过标记 `PluginPrivate` 冒充插件私有身份；非 collectible 私有上下文也不符合 City 的插件可卸载合同。该 descriptor 只能由后续 AUC-EVENTBUS-009 的插件领域运行时持有，Shared Registry 必须拒绝注册。

### 12. 插件之间通信

Plugin A 不应直接引用 Plugin B 的实现程序集或私有 Contract。

推荐关系：

```text
Plugin A
  -> DocumentExtension.Contracts

Plugin B
  -> DocumentExtension.Contracts

Host
  -> registers and loads DocumentExtension.Contracts
```

Plugin A 发布共享事件，Plugin B 订阅共享事件。双方只依赖 Contract，不依赖彼此实现，也不要求双方同时加载。

如果某个 contract 只存在于 Plugin A 私有 Assembly，它就只能用于 Plugin A 内部通信。

### 13. Capability 与访问控制

共享 Contract 不表示所有插件都自动获得发布和订阅权限。

Contract descriptor 可以声明：

- HostOnlyPublish。
- ModulePublish。
- PluginPublish。
- HostOnlySubscribe。
- PluginSubscribe。
- Required capability。
- Allowed channel。

Plugin manifest 必须声明：

- 需要引用的 Contract Assembly。
- 需要发布的 EventContractId。
- 需要订阅的 EventContractId。
- 需要的 channel。

Host 在插件加载前完成 capability 校验。未授权插件不能获得相应 publisher/subscriber contract。

### 14. 版本兼容

共享 Contract 必须采用显式兼容策略。

通常兼容的变更：

- 增加具有默认语义的可选字段。
- 增加新的 enum value，同时订阅方能处理 unknown value。
- 增加新的事件 contract。

破坏性变更：

- 删除字段。
- 修改字段类型。
- 修改字段必填性。
- 修改字段语义。
- 重命名稳定 Contract Id。
- 把嵌套类型替换成不兼容类型。

破坏性变更必须：

- 创建新的 EventContractId 或 major version。
- 保留旧 contract 的明确兼容周期。
- 在 Host manifest 中声明支持范围。
- 由应用或适配 handler 显式转换。

EventBus Core 不自动猜测 schema 兼容性。

### 15. Host 版本选择

Host 决定最终加载的共享 Contract 版本。

加载规则：

- Host 先加载并注册共享 Contract。
- 插件声明可接受版本范围。
- PluginSystem 验证 Host 版本是否满足范围。
- 插件不能私自加载另一版本并覆盖 Host。
- 多个插件要求互不兼容版本时，拒绝不兼容插件并输出诊断。

第一版不承诺同一 Contract Assembly 的多个 major version 在同一 Shared Plane 并存。需要并存时应使用不同 Assembly identity 和不同 EventContractId。

### 16. Manifest 与 Source Generator

Source Generator 为共享 Contract 生成：

- EventContractId。
- CLR type mapping。
- Contract version。
- Schema fingerprint。
- 嵌套 contract type graph。
- Channel metadata。
- Capability metadata。
- Strongly typed publisher descriptor。
- Strongly typed handler invoker descriptor。

构建期 analyzer 应检查：

- Contract Id 重复。
- 共享事件引用插件项目类型。
- 共享事件包含 `object`、delegate、UI type 或反射 type。
- Contract 类型不是稳定可访问类型。
- Contract version 缺失或不合法。
- 插件 manifest 未声明共享 Contract 依赖。

Dynamic Plugin Mode 读取插件预生成的 event manifest，不扫描任意类型。

### 17. 发布时校验

发布 Shared Plane 事件时，EventBus 使用已注册 descriptor 校验：

- EventContractId 是否存在。
- CLR type 是否与 descriptor 一致。
- 发布方是否有 capability。
- channel 是否允许。
- 插件是否仍处于 Active。

这些校验基于 descriptor 和整数/引用比较，不在热路径遍历 Assembly metadata。

插件尝试把私有事件发布到 Shared Plane 时，EventBus 必须拒绝并记录：

- PluginId。
- Event type。
- AssemblyLoadContext。
- Requested channel。
- Missing contract descriptor。

### 18. 卸载与缓存

Host 长期缓存只能保存 Shared Contract Plane 中由 Default ALC 加载的类型。

插件停用和卸载时必须清理：

- 插件 handler delegate。
- 插件 handler instance。
- 插件 subscription descriptor。
- 插件私有 EventContract descriptor。
- 插件私有 channel。
- 插件私有事件队列。
- 包含插件私有泛型参数的缓存。
- 插件 dispatch plan。

禁止在 Host 静态泛型缓存中永久保存：

```text
PluginPrivateEvent
IEventHandler<PluginPrivateEvent>
Func<PluginPrivateEvent,...>
```

Plugin Private Plane 的所有缓存必须由插件运行时上下文持有，并在卸载前整体释放。

### 19. Contract Assembly 卸载

Shared Contract Assembly 由 Default ALC 持有，生命周期通常与 Host 一致，不随单个插件卸载。

这意味着：

- 卸载 Plugin A 不会卸载共享 Contract Assembly。
- 共享事件对象可以在 Host 中正常存在。
- 必须清理的是 Plugin A 的 handler、delegate、队列和私有类型。

如果某个 Extension Contract 需要动态卸载，它就不能被作为 Host Shared Contract 使用。第一版不支持可卸载共享 Contract Assembly。

### 20. 错误处理

| 场景 | 处理 |
|---|---|
| Host 缺少插件需要的 Contract Assembly | 插件验证失败。 |
| Contract 版本不兼容 | 插件验证失败。 |
| Contract Id 冲突 | Host 构建或插件激活失败。 |
| 插件重复加载本地 Contract 副本 | 拒绝加载并输出类型身份诊断。 |
| 私有事件进入 Shared Plane | 拒绝发布。 |
| 共享事件对象图包含私有类型 | 构建期报错；运行时防御性拒绝。 |
| 插件无 publish/subscribe capability | 拒绝贡献或发布。 |

错误不能延迟为无法解释的 handler cast exception。

### 21. 测试要求

必须测试：

- Default ALC 和 Plugin ALC 中同名类型不相等。
- 插件依赖解析返回 Default ALC 中的共享 Assembly。
- 插件本地 Contract 副本不会被再次加载。
- 兼容版本插件可以加载。
- 不兼容版本插件在验证阶段失败。
- 私有事件不能发布到 Shared Plane。
- 共享事件完整对象图不能携带插件类型。
- Plugin A 和 Plugin B 能通过共享 Contract 通信。
- 卸载插件后 Shared Contract 仍可使用。
- 卸载插件后 Host 不再持有插件私有 `Type` 和 delegate。

### 22. 最终约束

任何需要在 Host、静态模块或多个插件之间发布和订阅的事件，其事件类型及完整对象图必须来自 Host 注册的共享 Contract 程序集，并由 Default AssemblyLoadContext 唯一加载。

插件私有事件类型只能在插件私有 EventBus 平面内使用，不得进入 Host 共享事件总线。
