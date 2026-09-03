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
- `ServiceAttribute` 只接受已定义的 `ServiceLifetime`；未知整数值不得静默降级为 Transient。
- `ScopedServiceAttribute` 与 `ExposeServicesAttribute` 的数组不得为 null 或包含 null；空数组继续表示没有显式 exposed contract。
- Generator 对未知 lifetime 或 null/invalid exposed type 产生 `AUCGEN005` 并停止 registrar 生成；metadata reader 不得过滤或修复非法输入。
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
| AUC-CORE-005 | DI Registration Markers | ServiceRegistrationAttributeTests; AtomUICityIncrementalGeneratorDependencyInjectionTests; GeneratedServiceRegistrationCatalogTests |
| AUC-CORE-006 | Host Diagnostics | HostDiagnosticsTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 路线图设计内容

当前 Core DI 合同覆盖 Root Provider 构建、Application ServiceScope、模块服务配置阶段和 `AUC-CORE-005` 的 registration markers。Plugin ServiceProvider、插件服务上下文、Host contract 白名单和 Contribution 服务来源尚未分配 Core Feature ID；这些内容属于 PluginSystem/能力模块路线图，不是当前 Core `Verified` 行为。

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

插件不能修改 Host Root ServiceProvider。独立 Plugin ServiceProvider 是 PluginSystem 的目标能力，当前 Core 不创建或持有该容器。

### 4. 核心规则

- 启动期模块可以在 ServiceProvider 构建前注册 Root `IServiceCollection`。
- 插件模块不得注册到 Host Root `IServiceCollection`；PluginSystem 负责为插件提供独立注册集合。
- 插件 Provider fallback 和 Host contract 白名单策略由 PluginSystem 定义，Core 当前不实现该解析层。
- 非测试 City 项目不允许直接调用 `BuildServiceProvider()`、`IServiceProviderFactory<T>.CreateServiceProvider` 或 Microsoft Generic Host 构建/启动入口；应用 Root Provider 只能由 City `ApplicationHost` 构建。
- 不允许从 Root Provider 解析 scoped 服务。
- 不允许服务实例把插件内部类型泄漏到 Host 长期持有对象中。
- `ValidateScopes` 在开发和测试环境默认开启。

### 5. 模块服务注册流程

启动期模块流程建议：

```text
PreConfigureServices(selected modules)
-> Generated service registration(selected module owners)
-> ConfigureServices(selected modules)
-> PostConfigureServices(selected modules)
-> User ConfigureServices(in registration order)
-> Build GenericHost
```

这样自动注册先建立默认服务，模块的 `ConfigureServices` 可以显式覆盖或调整，`PostConfigureServices` 完成模块内后置校正，应用级 `ConfigureServices` 最后追加、删除、替换或装饰 Root 服务。

`IApplicationHostBuilder` 不公开可变 `Services` 属性。应用扩展方法必须调用 `builder.ConfigureServices(...)`，不能依赖立即修改底层 Generic Host service collection。

插件模块目标流程（由 PluginSystem 实现）：

```text
Create plugin IServiceCollection
-> Register host contracts
-> Generated plugin service registration
-> Plugin module ConfigureServices
-> Build plugin ServiceProvider
```

PluginSystem 释放插件服务容器前，必须按各能力模块合同撤销领域 Lease 并关闭相关运行时 Scope；Core 当前不持有通用 ContributionLease。

### 6. 自动服务注册与所有权

可以提供自动服务注册，但默认必须是 source generator 自动注册，不是运行时扫描。

自动发现和运行时激活是两个不同阶段：Generator 可以发现当前编译及其引用中的服务清单，但 Host 只把最终 ModuleCatalog 已选依赖闭包所拥有的注册写入 Root `IServiceCollection`。所有已选静态模块共享同一个 Root Provider，不等于 Root Provider 接收所有引用程序集的服务。

每个包含自动服务声明的程序集必须在且仅在一个 `IModule` 实现上声明 `[ServiceRegistrationOwner]`：

```csharp
[ServiceRegistrationOwner]
public sealed class SalesModule : ModuleBase;
```

该 owner 必须由当前程序集声明，且当前程序集生成的 registrar 只能登记到这个本地 owner。一个 Module owner 只能由定义它的程序集及其唯一 generated registrar 持有；不相干的程序集不得把自己的业务服务登记到另一个程序集的 Module 名下。该 owner 是注册声明的唯一所有者，不是服务实例的所有者；实例仍由 DI 容器按 lifetime 创建和释放。

项目 A 使用项目 B 时，A 必须声明自己的 `AModule`，通过 `[DependsOn(typeof(BModule))]` 表达依赖，并让 A 的服务继续归属 `AModule`。只选择 `BModule` 不得激活 A 的任何服务；选择 `AModule` 才会同时激活依赖闭包中的 B 与 A。跨模块共享服务应归属一个明确的公共模块，由消费模块通过 `DependsOn` 显式依赖；不得通过冒充其他 owner 实现隐式扩展。未被应用根或 `UseModule<TModule>()` 的依赖闭包选中的 owner，其服务不会产生 Root `ServiceDescriptor`，不会参与冲突、`IEnumerable<T>`、验证或 `IHostedService` 启动。插件服务清单只能由 PluginSystem 应用到插件 Provider，Core Host 不把 plugin origin 注册注入 Root。

Registrar 图去重与 owner 所有权验证是两个不同步骤：同一个 registrar 经菱形引用多次到达时，必须按 registrar identity 幂等跳过；不同 registrar 声明同一个 owner 时属于所有权冲突，Host Build 必须确定性失败并报告 owner、原 registrar 与冲突 registrar，禁止按 owner 静默丢弃后到的注册。

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

生成代码示例（为便于阅读省略具体 descriptor 冲突策略）：

```csharp
public sealed class GeneratedServiceRegistrar
    : IServiceRegistrar
{
    public void Register(IServiceRegistrarContext context)
    {
        // 引用图边：Catalog 按 registrar type 去重，重复路径不会再次构造或执行 registrar。
        context.RegisterRegistrar(
            typeof(ReferencedGeneratedServiceRegistrar),
            static () => new ReferencedGeneratedServiceRegistrar());

        // 本地贡献：Catalog 将此 owner claim 绑定到当前实际 registrar，调用者不能伪造身份。
        context.Register(typeof(SalesModule), static services =>
        {
            services.AddScoped<IUserSession, UserSession>();
            services.AddSingleton<IClock, SystemClock>();
        });
    }
}
```

程序集级 `GeneratedServiceManifestAttribute` 把应用入口连接到根 registrar。每一条引用边通过 `RegisterRegistrar(Type, Func<IServiceRegistrar>)` 交还给 Catalog：Catalog 在调用 factory 前按 registrar type 去重，并校验 factory 返回实例的精确类型。每个 registrar 获得一个只在本次同步 `Register` 调用期间有效、且绑定到其真实类型的 context；保存 context 并在返回后调用会失败。`Register(owner, action)` 因而不能由调用者额外传入或伪造 registrar identity。

Host 在解析 ModuleCatalog 后按 owner 过滤，再按如下稳定阶段执行：

```text
PreConfigureServices(selected modules)
-> Generated service registration(selected module owners only)
-> ConfigureServices(selected modules)
-> PostConfigureServices(selected modules)
-> User ConfigureServices
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

服务 Attribute 的解释规则只有一份生产实现：`AtomUI.City.Generators` 中基于 Roslyn symbol 的 `ServiceRegistrationMetadataReader`。Core runtime 只保留 Attribute、marker interface、generated registrar bridge 和按已选 Module 应用 descriptor 的逻辑，不保留反射式 metadata reader 或测试替身，避免编译期与运行期规则分叉。

Analyzer 必须诊断：

- 自动服务程序集缺少 owner、存在多个 owner，或 owner 不是 `IModule`。
- generated registrar 的 owner 不是由该 registrar 所在程序集声明，或不同 registrar 争用同一个 owner。
- 多个公开构造函数但无明确构造函数选择。
- service id 或 exposed service 冲突。
- scoped 服务注入 singleton。
- 插件服务暴露为 Host 长期持有实例。
- 使用运行时扫描但未 opt-in。
- AOT Strict 下使用不可静态生成的工厂。
- `Replace` 与 `TryAdd` 同时启用。
- disposable 实现暴露多个 contract；在当前 net8/net10 DI disposal 语义下不生成可能造成重复释放的 forwarding alias。

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

### 10. Contribution 与服务来源（Deferred）

以下是未来跨模块 Contribution 服务来源合同的候选模型，当前 Core 不定义这些类型：

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

插件停用时的 Scope 反查、取消和容器释放顺序由 PluginSystem 与 Routing/Presentation 等能力模块共同实现，不属于当前 Core Host。

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
- 插件 Host contract 隔离和卸载后实例残留测试由 PluginSystem Feature 承担。
- 断言 scoped 服务不会从 Root Provider 解析。

产品级 DI 验收不得只使用单程序集示例。`fixtures/AtomUI.City.Core.Mvp` 以 8 个 owner 程序集、32 个服务声明和真实 CLI 进程覆盖跨程序集 registrar 图；其中相同 registrar 可经菱形项目引用被多次到达，Runtime Catalog 必须按 registrar identity 幂等跳过。不同 registrar 争用同一 owner 必须在 Build 阶段确定性失败，不能静默忽略；不同 owner 暴露同一 contract 且未声明 Replace/TryAdd 时仍必须确定性失败。
