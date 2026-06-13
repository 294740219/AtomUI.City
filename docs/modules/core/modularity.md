# AtomUI.City.Core Modularity 合同

## 适用范围

本专题属于 `AtomUI.City.Core` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Modularity` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.Core` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-CORE-001 | Application Host Builder | ApplicationHostBuilderTests; ApplicationHostRuntimeTests |
| AUC-CORE-002 | Lifecycle Pipeline | LifecycleMiddlewarePipelineTests; ApplicationHostLifecycleIntegrationTests |
| AUC-CORE-003 | Lifecycle Scope Tree | LifecycleScopeTreeTests |
| AUC-CORE-004 | Module Contract | ModuleAttributeTests; ModuleBaseTests; ModuleDescriptorTests |
| AUC-CORE-005 | DI Registration Markers | ServiceRegistrationAttributeTests |
| AUC-CORE-006 | Host Diagnostics | HostDiagnosticsTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Core Modularity 设计

适用范围：`AtomUI.City.Core` 中模块定义、依赖声明、模块图、模块生命周期、模块贡献、插件模块接入、AOT/source generator 约束。

### 1. 定位

Module 是 AtomUI.City 应用能力组织的基本单位。

Module 用于声明一组框架能力，包括服务注册、配置、路由、权限、本地化、事件处理、数据客户端、Presentation 资源、命令、诊断提供者和插件扩展点。

Module 不是生命周期 Scope，不是 DI Scope，也不是 Plugin。Module 是应用组成单元和能力贡献方。运行时实例生命周期由 Lifecycle Scope 管理，模块贡献能力通过 Contribution 和 ContributionLease 进入 Host。

```text
Application
  Modules
  Plugins
    Plugin
      Modules
```

### 2. 设计目标

- 提供统一模块编程模型。
- 支持模块依赖声明和确定性启动顺序。
- 支持编译期生成模块清单和依赖图输入。
- 支持启动期模块和插件模块共用同一套模块接口。
- 支持模块服务注册、配置、初始化、启动、关闭。
- 支持模块通过 Contribution 贡献可撤销能力。
- 保持 AOT/trimming/source generator 友好。
- 保持 Core 不依赖 UI、MVVM、Routing、PluginSystem 的具体实现。

### 3. 非目标

Modularity 不负责：

- UI 控件、窗口、View/ViewModel 绑定。
- 路由匹配和导航执行。
- 权限策略判定。
- 数据请求管线。
- 状态管理。
- 插件程序集加载。
- ViewModel Activation。
- 业务模块分层规范。

这些由对应模块实现。Modularity 只提供模块契约、模块图和生命周期调度。

### 4. 核心概念

| 概念 | 职责 |
|---|---|
| `Module` | 模块基类，提供模块生命周期方法。 |
| `ModuleAttribute` | 可选模块元数据。未指定 id 时使用模块类型全名。 |
| `DependsOnAttribute` | 声明模块依赖，由 source generator 读取。 |
| `ModuleDescriptor` | 模块不可变描述信息。 |
| `ModuleManifest` | 编译期生成或插件包携带的模块清单。 |
| `ModuleGraph` | 解析后的模块依赖图。 |
| `ModuleCatalog` | 当前 Host 可见模块集合。 |
| `ModuleRegistry` | 当前 Host 已加载模块状态和诊断信息。 |
| `ModuleLifecycleContext` | 模块生命周期执行上下文。 |

### 5. Module 基类

模块统一继承 `Module`。插件模块也继承同一个 `Module`，不设计单独的 `PluginModule` 公共基类。

```csharp
public abstract class Module
{
    public virtual ValueTask PreConfigureServicesAsync(
        ServiceConfigurationContext context,
        CancellationToken cancellationToken = default)
    {
        PreConfigureServices(context);
        return ValueTask.CompletedTask;
    }

    public virtual void PreConfigureServices(ServiceConfigurationContext context) { }

    public virtual ValueTask ConfigureServicesAsync(
        ServiceConfigurationContext context,
        CancellationToken cancellationToken = default)
    {
        ConfigureServices(context);
        return ValueTask.CompletedTask;
    }

    public virtual void ConfigureServices(ServiceConfigurationContext context) { }

    public virtual ValueTask PostConfigureServicesAsync(
        ServiceConfigurationContext context,
        CancellationToken cancellationToken = default)
    {
        PostConfigureServices(context);
        return ValueTask.CompletedTask;
    }

    public virtual void PostConfigureServices(ServiceConfigurationContext context) { }

    public virtual ValueTask ConfigureContributionsAsync(
        ContributionConfigurationContext context,
        CancellationToken cancellationToken = default)
    {
        ConfigureContributions(context);
        return ValueTask.CompletedTask;
    }

    public virtual void ConfigureContributions(ContributionConfigurationContext context) { }

    public virtual ValueTask OnPreApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken = default)
    {
        OnPreApplicationInitialization(context);
        return ValueTask.CompletedTask;
    }

    public virtual void OnPreApplicationInitialization(ApplicationInitializationContext context) { }

    public virtual ValueTask OnApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken = default)
    {
        OnApplicationInitialization(context);
        return ValueTask.CompletedTask;
    }

    public virtual void OnApplicationInitialization(ApplicationInitializationContext context) { }

    public virtual ValueTask OnPostApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken = default)
    {
        OnPostApplicationInitialization(context);
        return ValueTask.CompletedTask;
    }

    public virtual void OnPostApplicationInitialization(ApplicationInitializationContext context) { }

    public virtual ValueTask OnApplicationShutdownAsync(
        ApplicationShutdownContext context,
        CancellationToken cancellationToken = default)
    {
        OnApplicationShutdown(context);
        return ValueTask.CompletedTask;
    }

    public virtual void OnApplicationShutdown(ApplicationShutdownContext context) { }
}
```

运行时只调用 async 方法。同步方法只是开发便利入口。

### 6. 模块标识

模块 id 默认使用模块类型全名。

```csharp
namespace AtomUI.City.Routing;

public sealed partial class RoutingModule : Module
{
}
```

默认模块 id：

```text
AtomUI.City.Routing.RoutingModule
```

`ModuleAttribute` 是可选的，只用于覆盖 id 或补充元数据。

```csharp
[Module(Version = "1.0.0", Description = "Routing module")]
public sealed partial class RoutingModule : Module
{
}
```

显式 id 只在需要稳定公开 id、跨版本兼容、插件发布或清单对外暴露时使用。

### 7. 模块依赖

模块依赖通过 Attribute 声明。

```csharp
[DependsOn(typeof(RoutingModule))]
[DependsOn(typeof(SecurityModule))]
public sealed partial class AppModule : Module
{
}
```

可选依赖：

```csharp
[DependsOn(typeof(LocalizationModule), Optional = true)]
public sealed partial class PresentationModule : Module
{
}
```

依赖声明使用 `typeof(TModule)`，不使用字符串。字符串 id 只用于模块标识，不作为依赖引用的主路径。

### 8. Source Generator 模块图

模块依赖图由 source generator 在编译期建立输入。

编译期流程：

```text
Find Module-derived types
-> Read ModuleAttribute
-> Read DependsOnAttribute
-> Generate ModuleDescriptor
-> Generate ModuleManifest
-> Generate module dependency graph input
-> Emit diagnostics
```

运行时流程：

```text
ApplicationHost
-> Load generated ModuleManifest
-> Resolve startup modules
-> Validate ModuleGraph
-> Topological sort
-> Run module lifecycle
```

默认不允许运行时扫描程序集寻找模块。动态发现只能作为 opt-in fallback，并必须输出 AOT/trimming 诊断。

### 9. 模块类型

第一版区分两种来源：

| 来源 | 说明 |
|---|---|
| Application | 随应用启动加载的静态模块。 |
| Plugin | 插件携带的普通模块。 |

插件模块不特殊化 API。它通过 descriptor 表达来源：

```text
ModuleDescriptor.Origin = Plugin
ModuleDescriptor.Plugin = PluginDescriptor
```

插件模块的生命周期、依赖、贡献方式与普通模块一致，但 Host 会套用插件隔离、ContributionLease 和卸载规则。

### 10. 生命周期阶段

模块启动阶段：

```text
Discovered
-> GraphResolved
-> PreConfigureServices
-> ConfigureServices
-> PostConfigureServices
-> ConfigureContributions
-> OnPreApplicationInitialization
-> OnApplicationInitialization
-> OnPostApplicationInitialization
-> Running
```

模块关闭阶段：

```text
OnApplicationShutdown
-> Revoke contribution leases
-> Dispose module resources
-> Stopped
```

启动顺序按拓扑排序执行：依赖先启动，被依赖方后启动。关闭顺序反向执行。

### 11. 阶段语义

| 阶段 | 职责 |
|---|---|
| `PreConfigureServices` | 注册早期配置、Options 默认值、能力开关。 |
| `ConfigureServices` | 注册服务描述。禁止构建 ServiceProvider。 |
| `PostConfigureServices` | 对服务注册和 Options 做后置调整。 |
| `ConfigureContributions` | 声明路由、权限、本地化、命令、事件处理等贡献。 |
| `OnPreApplicationInitialization` | ServiceProvider 可用后的早期初始化。 |
| `OnApplicationInitialization` | 模块主初始化。 |
| `OnPostApplicationInitialization` | 所有模块主初始化之后的后置初始化。 |
| `OnApplicationShutdown` | 应用关闭或插件停用时的模块关闭。 |

`PreConfigureServices`、`ConfigureServices`、`PostConfigureServices` 发生在 ServiceProvider 构建前。
`ConfigureContributions` 发生在 ServiceProvider 可用后，由 Host 统一校验并返回 ContributionLease。

### 12. DI 规则

启动期模块可以注册 Root `IServiceCollection`，但只能发生在 ServiceProvider 构建前。

插件模块不能修改 Host Root ServiceProvider。插件模块的服务注册进入插件服务上下文，并通过 Host contract 受控暴露。

模块规则：

- 不允许在服务配置阶段调用 `BuildServiceProvider()`。
- 不允许在模块构造函数中解析服务。
- 不允许把模块实例作为全局状态使用。
- 服务覆盖必须可诊断。
- 插件服务不能泄漏为 Host 长期持有实例。

### 13. Contribution 规则

Module 不直接修改全局 registry。Module 通过 Contribution Request 声明能力，由 Host 校验后进入目标 registry，并返回 ContributionLease。

```text
Module
-> Contribution Request
-> Host validation
-> Target Registry
-> ContributionLease
```

模块可贡献：

- Routes。
- Permissions。
- Localization resources。
- EventBus handlers。
- Commands / Actions。
- Data clients。
- Presentation resources。
- Diagnostics providers。
- Plugin extension points。

插件模块的所有运行时贡献必须可撤销。插件停用时 Host 按反向顺序撤销 ContributionLease。

### 14. 错误策略

启动期 required 模块失败：Host 启动失败，并输出模块 id、类型、阶段、依赖图和诊断上下文。

optional 模块失败：默认禁用该模块并记录 warning，是否继续由 Host policy 决定。

插件模块失败：插件激活失败，已创建的 ContributionLease 必须回滚，插件服务上下文释放，主应用继续运行。

关闭失败：进入错误处理管线，继续尝试关闭后续模块并聚合诊断。

### 15. AOT 约束

Modularity 默认 AOT-first。

要求：

- 模块发现依赖 source generator 生成清单。
- 模块依赖图由编译期生成输入。
- 模块工厂使用强类型生成代码。
- Analyzer 检测重复 id、循环依赖、缺失依赖和动态扫描。
- Runtime 不依赖反射扫描作为默认路径。

禁止默认行为：

- 启动时扫描所有程序集找模块。
- 通过反射读取模块元数据作为主路径。
- 动态代理生成模块实例。
- Generator 执行用户代码。

### 16. 测试策略

Testing 包应支持：

- 构造 ModuleDescriptor。
- 构造 ModuleGraph。
- 断言拓扑排序。
- 模拟重复模块 id。
- 模拟缺失依赖和循环依赖。
- 驱动模块生命周期。
- 断言服务配置阶段没有构建 ServiceProvider。
- 断言 ContributionLease 创建和反向撤销。
- 断言插件模块失败时能回滚。
