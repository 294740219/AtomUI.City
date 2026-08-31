# AtomUI.City.Core Dependency Injection 合同

## 适用范围

本专题属于 `AtomUI.City.Core` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Dependency Injection` 相关实现决策，不重新定义模块边界。

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

## AtomUI.City.Core Dependency Injection 设计

适用范围：`AtomUI.City.Core` 中服务注册、服务作用域、模块服务注册、插件服务隔离、自动服务注册、AOT/source generator 约束。

### 1. 定位

Dependency Injection 是 AtomUI.City Host 的基础设施之一。AtomUI.City 默认复用 `Microsoft.Extensions.DependencyInjection` 和 GenericHost，不重造 DI 容器。

DI 模块负责定义：

- 模块如何注册服务。
- Host Root ServiceProvider 的边界。
- Application、Plugin、Route、Activation、Operation 等生命周期如何拥有服务作用域。
- 插件服务如何隔离。
- Contribution 如何绑定服务来源。
- 自动服务注册如何做到 AOT 友好。

### 2. 非目标

DI 不负责业务分层，不提供领域服务规范，不替换 `Microsoft.Extensions.DependencyInjection`，不承诺支持任意第三方容器的全部特性。

Core DI 也不负责 ViewModel 创建、路由解析、插件程序集加载和 Data client 代理生成，这些由对应模块接入服务解析能力。

### 3. 服务容器层级

AtomUI.City 第一版建议明确三类服务上下文：

```text
Host Root ServiceProvider
  -> Application ServiceScope
  -> Lifecycle-owned ServiceScopes

Plugin ServiceProvider
  -> Plugin-owned ServiceScopes
```

Host Root ServiceProvider 由 GenericHost 构建，承载框架核心服务、启动期模块服务和应用固定服务。

Application ServiceScope 随 ApplicationScope 创建和释放，用于应用生命周期内的 scoped 服务。

Host 启动后，`ConfigureContributions`、三个 application initialization hook 和 `OnApplicationShutdown` 接收同一个 Application ServiceScope provider。模块关闭完成后释放该 ServiceScope；`IApplicationHost.Services` 始终保持为 GenericHost root provider。

Lifecycle-owned ServiceScope 由 RouteScope、ActivationScope、OperationScope 等运行时 Scope 按需创建和释放。

Plugin ServiceProvider 是插件独立服务容器。插件不能修改 Host Root ServiceProvider。

### 4. 核心规则

- 启动期模块可以在 ServiceProvider 构建前注册 Root `IServiceCollection`。
- 插件模块只能注册到插件自己的 `IServiceCollection`。
- 插件服务不能自动 fallback 到 Host Root ServiceProvider。
- 插件需要访问 Host 能力时，只能通过 Host 显式暴露的 contract。
- 非测试 City 项目不允许直接调用 `BuildServiceProvider()`、`IServiceProviderFactory<T>.CreateServiceProvider` 或 Microsoft Generic Host 构建/启动入口；应用 Root Provider 只能由 City `ApplicationHost` 构建。
- 不允许从 Root Provider 解析 scoped 服务。
- 不允许服务实例把插件内部类型泄漏到 Host 长期持有对象中。
- `ValidateScopes` 在开发和测试环境默认开启。

### 5. 模块服务注册流程

启动期模块流程建议：

```text
PreConfigureServices(all modules)
-> Generated service registration(all modules)
-> ConfigureServices(all modules)
-> PostConfigureServices(all modules)
-> User ConfigureServices(in registration order)
-> Build GenericHost
```

这样自动注册先建立默认服务，模块的 `ConfigureServices` 可以显式覆盖或调整，`PostConfigureServices` 完成模块内后置校正，应用级 `ConfigureServices` 最后追加、删除、替换或装饰 Root 服务。

`IApplicationHostBuilder` 不公开可变 `Services` 属性。应用扩展方法必须调用 `builder.ConfigureServices(...)`，不能依赖立即修改底层 Generic Host service collection。

插件模块流程：

```text
Create plugin IServiceCollection
-> Register host contracts
-> Generated plugin service registration
-> Plugin module ConfigureServices
-> Build plugin ServiceProvider
```

插件服务容器释放前必须先撤销该插件产生的 ContributionLease，并关闭相关运行时 Scope。

### 6. 自动服务注册

可以提供自动服务注册，但默认必须是 source generator 自动注册，不是运行时扫描。

推荐三种注册方式：

```csharp
[ScopedService(typeof(IUserSession))]
public sealed class UserSession : IUserSession
{
}
```

```csharp
[Service(ServiceLifetime.Singleton)]
[ExposeServices(typeof(IClock))]
public sealed class SystemClock : IClock
{
}
```

```csharp
public sealed class CacheStore : ISingletonDependency
{
}
```

建议优先支持 Attribute，Marker Interface 作为简写。原因是 Attribute 更明确，能表达 exposed service、lifetime、replace、try-add、keyed service 等元数据。

### 7. Source Generator 注册模型

编译期流程：

```text
Find service candidate types
-> Read service attributes / marker interfaces
-> Validate constructors and exposed services
-> Generate service registration code
-> Emit service manifest
-> Emit diagnostics
```

生成代码示例：

```csharp
internal static class GeneratedServiceRegistrar
{
    public static void Register(IServiceCollection services)
    {
        services.AddScoped<IUserSession, UserSession>();
        services.AddSingleton<IClock, SystemClock>();
    }
}
```

Strict AOT 模式下可以进一步生成强类型 factory：

```csharp
services.AddScoped<IUserSession>(sp =>
    new UserSession(sp.GetRequiredService<IClock>()));
```

这可以减少对运行时反射构造的依赖，但要求 generator 能明确选择构造函数并解析依赖。

### 8. 自动注册约束

默认禁止：

- 启动时扫描所有程序集找服务。
- 通过反射读取服务 Attribute 再注册。
- 基于命名约定运行时发现服务。
- 动态代理作为默认服务注册方式。
- Property injection 作为默认能力。

Analyzer 必须诊断：

- 多个公开构造函数但无明确构造函数选择。
- service id 或 exposed service 冲突。
- scoped 服务注入 singleton。
- 插件服务暴露为 Host 长期持有实例。
- 使用运行时扫描但未 opt-in。
- AOT Strict 下使用不可静态生成的工厂。

### 9. 服务覆盖策略

手写 `ConfigureServices` 遵循 `Microsoft.Extensions.DependencyInjection` 的基本语义。

生成注册需要更严格：

- 默认使用普通 `Add*` 还是 `TryAdd*` 需要由 attribute 指定或框架约定。
- 自动注册发现同一 service type 多个实现时，默认报诊断。
- 多实现服务必须显式声明允许 `IEnumerable<T>`。
- 替换服务必须显式使用 replace 语义。
- 静默覆盖不允许作为默认行为。

示例：

```csharp
[ScopedService(typeof(IUserSession), Replace = true)]
public sealed class CustomUserSession : IUserSession
{
}
```

### 10. Contribution 与服务来源

每个 Contribution 必须记录服务来源：

```text
Contribution
  Module
  Plugin?
  ServiceContext
  Lease
```

启动期模块贡献使用 Application 服务上下文。
插件模块贡献使用 Plugin 服务上下文。

例如插件贡献路由时，RouteScope 创建 ViewModel 应从插件 ServiceProvider 创建 route/activation service scope，而不是从 Host Root 解析。

```text
RouteContribution("/sales")
  Plugin = SalesPlugin
  Services = SalesPlugin ServiceProvider
```

插件停用时，Host 根据 Contribution 找到仍在运行的 RouteScope、ActivationScope、OperationScope，先取消和释放，再释放插件服务容器。

### 11. 公共抽象建议

| 类型 | 职责 |
|---|---|
| `ServiceConfigurationContext` | 模块服务配置上下文。 |
| `ServiceRegistrationDescriptor` | 编译期服务注册描述。 |
| `GeneratedServiceRegistrar` | SG 生成的服务注册入口。 |
| `ServiceContext` | 当前服务来源和 ServiceProvider 包装。 |
| `ServiceContextKind` | Root、Application、Plugin、Route、Activation、Operation。 |
| `IServiceContextAccessor` | 当前生命周期中访问服务上下文。 |
| `IHostContractRegistry` | Host 显式暴露给插件的 contract 集合。 |

### 12. 测试策略

Testing 包应支持：

- 构造测试 Host 并替换服务。
- 断言模块服务注册顺序。
- 断言自动注册生成结果。
- 断言重复注册和覆盖诊断。
- 断言插件不能解析未暴露 Host 服务。
- 断言插件卸载后无服务实例残留。
- 断言 scoped 服务不会从 Root Provider 解析。
