# AtomUI.City.Core API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Hosting | ApplicationHost, ApplicationHostBuilder, IApplicationHost, IApplicationContext | 构建和持有应用 Host。 | Build 后冻结；Start/Stop 幂等；Dispose 后 mutating API 失败。 |
| Lifecycle | LifecycleStage, LifecyclePipeline, LifecycleScope | 阶段、中间件和所有权树。 | 按 stage 稳定执行；leaf-first 释放；异常进入 diagnostics。 |
| Modularity | ModuleBase, IModule, ModuleDescriptor, ModuleOrigin, DependsOnAttribute, ServiceConfigurationContext, ModuleServiceCollection | 模块声明、依赖、配置阶段和生命周期钩子。 | 配置、服务注册、初始化、关闭严格分阶段；PreConfigure 按模块拓扑顺序执行；配置阶段对象结束后冻结。 |
| DependencyInjection | ServiceAttribute, ScopedServiceAttribute, ExposeServicesAttribute, dependency marker interfaces | AOT 友好的服务声明。 | 不依赖运行时扫描作为唯一发现方式。 |
| Threading | IUiDispatcher, UnavailableUiDispatcher | UI 调度抽象。 | Core 不绑定 UI 平台。 |
| Diagnostics | IHostDiagnostics, HostDiagnosticRecord, HostDiagnosticIds | Host 级诊断。 | 现有诊断码保持兼容，目标诊断码新增需迁移。 |

## 首批冻结 API Cards

以下 API 是 Phase 0 到 Phase 3 的产品级冻结范围。没有进入本节的 public 类型可以保留为支持类型，但不能在首批实现中被当作稳定产品 API 对外承诺。

### `ApplicationHost`

| Field | Contract |
| --- | --- |
| Type | `ApplicationHost` |
| Namespace | `AtomUI.City.Hosting` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-001 |
| Purpose | 提供 Host 静态入口，创建 `ApplicationHostBuilder`。 |
| Owner | Build process / application startup caller |
| Created By | 调用方直接调用静态方法。 |
| Lifetime | 静态入口无运行期状态。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 静态创建方法必须无共享可变状态，可并发调用创建独立 builder。 |
| Disposal | 类型本身无释放；创建出的 builder/host 按各自合同释放。 |
| Nullability | `args` 可以为空；配置 delegate 不得为空。 |
| Cancellation | 无异步取消。 |
| Failure Behavior | 参数非法抛 `ArgumentNullException` 或 `ArgumentException`。 |
| Diagnostics | 创建 builder 不写诊断；Build 阶段写 HostBuilt 诊断。 |
| Plugin Boundary | 插件不能通过该入口创建嵌套 Host 修改当前 Host。 |
| AOT / Trimming | 不执行程序集扫描，不依赖 reflection discovery。 |
| Breaking Change Rules | 删除入口、修改默认 options 或改变异常类型属于 breaking change。 |
| Tests | `ApplicationHostBuilderTests` 断言 builder 独立、默认 options、非法参数。 |

### `ApplicationHostBuilder`

| Field | Contract |
| --- | --- |
| Type | `ApplicationHostBuilder` |
| Namespace | `AtomUI.City.Hosting` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-001 |
| Purpose | 收集 options、services、modules、configuration 和 lifecycle middleware，并构建 Host。 |
| Owner | Application startup caller |
| Created By | `ApplicationHost.CreateBuilder`。 |
| Lifetime | Created -> Configuring -> Built -> Frozen；Build 后不可变。 |
| DI Lifetime | 不进入 DI；构建后生成 root service provider。 |
| Thread Safety | 配置阶段不保证线程安全；Build 必须串行；Build 后所有 mutating API 必须失败。 |
| Disposal | Builder 不拥有已构建 Host；Host 由调用方释放。 |
| Nullability | 注册 delegate、module type、service action 不得为空。 |
| Cancellation | Build 为同步或短异步准备阶段；不接受 token 的 API 不执行可取消 IO。 |
| Failure Behavior | 重复 Build、Build 后继续注册、无效 module/options 抛声明异常或返回失败 Result。 |
| Diagnostics | Build 成功写 `AUCHOST001`；Build 失败写结构化失败诊断。 |
| Plugin Boundary | 插件不能写 Host root builder；插件模块只能通过 PluginSystem 贡献。 |
| AOT / Trimming | 默认只消费显式注册或 generated manifest，不把运行时扫描作为唯一路径。 |
| Breaking Change Rules | 修改冻结时机、默认 services、module graph 规则或 Build 失败策略属于 breaking change。 |
| Tests | `ApplicationHostBuilderTests` 断言冻结、重复 Build、无效 options、诊断上下文。 |

### `IApplicationHost`

| Field | Contract |
| --- | --- |
| Type | `IApplicationHost` |
| Namespace | `AtomUI.City.Hosting` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-001, AUC-CORE-002 |
| Purpose | 表达应用 Host 的启动、停止、上下文访问和释放 contract。 |
| Owner | Application root owner |
| Created By | `ApplicationHostBuilder.Build`。 |
| Lifetime | Created -> Starting -> Running -> Stopping -> Stopped -> Disposed；失败进入 Faulted 后必须可 Stop/Dispose。 |
| DI Lifetime | Root object；不作为普通 scoped service 创建。 |
| Thread Safety | `StartAsync` 并发调用必须串行或拒绝；`StopAsync` 并发调用合并为同一停止事务；查询上下文可并发读取。 |
| Disposal | `Dispose` 和 `DisposeAsync` 幂等；Dispose 后 mutating API 失败，immutable context 可读取。 |
| Nullability | `CancellationToken` 可为默认值；返回值不得为空。 |
| Cancellation | `StartAsync` 观察 token；`StopAsync` 的取消只影响等待，不跳过最小清理。 |
| Failure Behavior | middleware、module initialization 或 shutdown 失败进入 diagnostics；Host 状态必须稳定；进入 Stopped 后再次 Start 抛 `InvalidOperationException`。 |
| Diagnostics | Start/Stop 写 `AUCHOST002`、`AUCHOST003` 和失败诊断。 |
| Plugin Boundary | Host 停止时必须请求插件来源 owner 释放，但不直接加载插件程序集。 |
| AOT / Trimming | Host 启动不执行默认程序集扫描。 |
| Breaking Change Rules | 修改状态机、幂等规则、取消语义或诊断码语义属于 breaking change。 |
| Tests | `ApplicationHostRuntimeTests` 断言 Start/Stop 幂等、Faulted 清理、Dispose 后行为。 |

### `IApplicationContext`

| Field | Contract |
| --- | --- |
| Type | `IApplicationContext` |
| Namespace | `AtomUI.City.Hosting` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-001 |
| Purpose | 暴露应用 id、环境、root scope、diagnostics 和服务访问上下文。 |
| Owner | Application Host |
| Created By | Host Build 阶段。 |
| Lifetime | 随 Host 创建，Host Dispose 后只允许读取 immutable descriptor。 |
| DI Lifetime | Singleton。 |
| Thread Safety | immutable 字段可并发读取；mutable runtime state 必须由专门服务管理。 |
| Disposal | 不单独释放；随 Host 释放。 |
| Nullability | `ApplicationId`、`EnvironmentName`、`RootScope` 不得为空。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 缺失必填上下文时 Build 失败。 |
| Diagnostics | Build 失败诊断包含缺失字段名。 |
| Plugin Boundary | 插件只可读取 Host 暴露的共享 context，不能获得 Host 内部可变对象。 |
| AOT / Trimming | Context 字段不依赖动态类型发现。 |
| Breaking Change Rules | 删除字段、改变字段含义或把可读字段改为延迟失败属于 breaking change。 |
| Tests | `ApplicationHostRuntimeTests` 断言 root scope、diagnostics 和 context 可读取。 |

### `LifecyclePipeline`

| Field | Contract |
| --- | --- |
| Type | `LifecyclePipeline` |
| Namespace | `AtomUI.City.Lifecycle` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-002 |
| Purpose | 按稳定 stage 顺序执行 lifecycle middleware。 |
| Owner | Application Host |
| Created By | `ApplicationHostBuilder.Build`。 |
| Lifetime | 随 Host 创建和释放。 |
| DI Lifetime | Singleton。 |
| Thread Safety | pipeline 构建后不可变；执行阶段必须由 Host 串行调度。 |
| Disposal | pipeline 本身无外部资源；middleware 释放通过 scope 处理。 |
| Nullability | stage、context、middleware delegate 不得为空。 |
| Cancellation | 每个 middleware 调用前后观察 token；取消不得跳过 cleanup stage。 |
| Failure Behavior | middleware 异常进入 Faulted，错误聚合后继续必要清理。 |
| Diagnostics | 失败诊断包含 stage、middleware type、operationId。 |
| Plugin Boundary | 插件 middleware 必须绑定 owner；插件停用时撤销后不再执行。 |
| AOT / Trimming | middleware 来源优先显式注册或 generated manifest。 |
| Breaking Change Rules | 修改 stage 顺序、同 stage 排序、异常聚合或取消语义属于 breaking change。 |
| Tests | `LifecycleMiddlewarePipelineTests` 断言 stage 顺序、同 stage 顺序、异常和取消路径。 |

### `LifecycleScope`

| Field | Contract |
| --- | --- |
| Type | `LifecycleScope` |
| Namespace | `AtomUI.City.Lifecycle` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-003 |
| Purpose | 表达 application、module、plugin contribution、route、activation 和 operation 的所有权树。 |
| Owner | Parent scope 或 Host root |
| Created By | Host、ModuleSystem 或上层运行时模块。 |
| Lifetime | Active -> Disposing -> Disposed。 |
| DI Lifetime | Scope object；可被对应 owner 持有，不注册为 root singleton。 |
| Thread Safety | child 创建和 dispose 必须同步保护；读取状态可并发；mutating API 在 Disposing/Disposed 失败。 |
| Disposal | leaf-first；重复 Dispose 幂等；child 释放失败记录诊断并继续释放其他 child。 |
| Nullability | scope id、kind、owner 不得为空。 |
| Cancellation | Dispose 不启动新 operation；active operation 必须在 parent stop 时被取消。 |
| Failure Behavior | parent disposed 后创建 child 失败；释放失败聚合到 diagnostics。 |
| Diagnostics | 诊断包含 scopeId、kind、owner、child scope 和 exception type。 |
| Plugin Boundary | plugin owner 停用时必须释放该插件创建或贡献的 scope。 |
| AOT / Trimming | scope 不保存插件私有类型作为 Host 长期 key。 |
| Breaking Change Rules | 修改释放顺序、幂等行为或 Dispose 后访问规则属于 breaking change。 |
| Tests | `LifecycleScopeTreeTests` 断言 leaf-first、parent-child 状态、重复释放和失败诊断。 |

### `ModuleBase`

| Field | Contract |
| --- | --- |
| Type | `ModuleBase` |
| Namespace | `AtomUI.City.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 为模块配置、服务注册、初始化和关闭提供基类 contract。 |
| Owner | Module system |
| Created By | Module registry 或 DI。 |
| Lifetime | Declared -> ServicesConfigured -> Initialized -> Shutdown。 |
| DI Lifetime | 默认 transient 或由 module registry 控制；不得作为 Host root mutable singleton 暴露给插件。 |
| Thread Safety | 单个 module 实例的 lifecycle hook 由 module system 串行调用。 |
| Disposal | 如果 module 实例实现 dispose，Host shutdown 按 module 逆序释放。 |
| Nullability | context 参数不允许为空。 |
| Cancellation | 所有 async hook 在进入同步便利方法前必须观察 token；Initialize/Shutdown 异步 hook 必须观察 token。 |
| Failure Behavior | hook 抛异常时当前阶段失败并记录 module 诊断；shutdown 继续后续 module 清理。 |
| Diagnostics | 诊断包含 module id、module type、stage。 |
| Plugin Boundary | 插件 module 只能写插件 service scope 和受控 contribution。 |
| AOT / Trimming | 模块发现默认依赖显式注册或 generated manifest。 |
| Breaking Change Rules | 修改 hook 顺序、context 能力或默认 module id 规则属于 breaking change。 |
| Tests | `ModuleBaseTests` 断言 hook 顺序、null context、取消、异常和 shutdown 行为。 |

### `ModuleDescriptor`

| Field | Contract |
| --- | --- |
| Type | `ModuleDescriptor` |
| Namespace | `AtomUI.City.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 描述模块 id、type、依赖、origin、plugin owner 和 source。 |
| Owner | Module registry |
| Created By | Attribute reader、generated manifest 或显式注册。 |
| Lifetime | immutable descriptor；可在 Host 生命周期内共享读取。 |
| DI Lifetime | 不作为服务实例注册。 |
| Thread Safety | 不可变，可并发读取。 |
| Disposal | 无释放。 |
| Nullability | module id、module type、origin 不得为空；plugin origin 必须提供 plugin id；application origin 不允许提供 plugin id。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 缺失 id/type、非 `IModule` 类型、重复 id、循环依赖、缺失依赖或非法 origin 导致 graph build 失败。 |
| Diagnostics | 诊断包含 module id、type、dependency path。 |
| Plugin Boundary | 插件 module descriptor 必须设置 `Origin = Plugin` 并包含稳定 plugin id；application module 不得携带 plugin id。 |
| AOT / Trimming | 支持 generator 输出，不要求运行时扫描。 |
| Breaking Change Rules | 修改默认 id、依赖排序或 descriptor 字段含义属于 breaking change。 |
| Tests | `ModuleDescriptorTests` 断言默认 id、显式 id、依赖排序、origin、plugin id、非模块类型和错误诊断。 |

### `ModuleOrigin`

| Field | Contract |
| --- | --- |
| Type | `ModuleOrigin` |
| Namespace | `AtomUI.City.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 标识模块描述来自应用静态模块还是插件模块。 |
| Owner | Module descriptor |
| Created By | `ModuleDescriptor` 构造或 generated manifest。 |
| Lifetime | enum metadata，无运行期状态。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 无 mutable state。 |
| Disposal | 无释放。 |
| Nullability | 不适用；未知 enum 值必须被 descriptor 拒绝。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | `ModuleDescriptor` 遇到未知 origin 值抛 `ArgumentOutOfRangeException`。 |
| Diagnostics | graph build 失败诊断必须包含 module id 和 origin。 |
| Plugin Boundary | `Plugin` origin 必须和 plugin id 成对出现。 |
| AOT / Trimming | enum 常量可由 generator 直接输出。 |
| Breaking Change Rules | 删除、重命名或改变 enum 值语义属于 breaking change。 |
| Tests | `ModuleDescriptorTests` 断言默认 application origin 和 plugin origin 校验。 |

### `ServiceConfigurationContext`

| Field | Contract |
| --- | --- |
| Type | `ServiceConfigurationContext` |
| Namespace | `AtomUI.City.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 在模块服务配置阶段提供 application context、受控 service collection 和 early options preconfigure store。 |
| Owner | Module registry |
| Created By | `ModuleRegistry.ConfigureServicesAsync` 或测试代码。 |
| Lifetime | 只在 PreConfigureServices、ConfigureServices、PostConfigureServices 阶段有效。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 与 module configuration 调度一致，由 module registry 串行调用；不保证并发写安全。 |
| Disposal | 无外部资源；阶段结束后内部 `ModuleServiceCollection` 冻结。 |
| Nullability | application context、services、preconfigure action 和 options instance 不得为空。 |
| Cancellation | context 本身无取消；调用方 async hook 在进入阶段前观察 token。 |
| Failure Behavior | null action 或 null options 抛 `ArgumentNullException`；options action 异常由当前 module stage 诊断承接。 |
| Diagnostics | action 异常由 module registry 写 `AUCHOST106`，context 包含 moduleType 和 stage。 |
| Plugin Boundary | 插件模块使用插件自己的 context 和 preconfigure store，不能修改 Host 全局 store。 |
| AOT / Trimming | 不扫描程序集，不读取 runtime attributes；只执行显式注册的同步 action。 |
| Breaking Change Rules | 修改 PreConfigure 顺序、null 行为、阶段生命周期或冻结时机属于 breaking change。 |
| Tests | `ApplicationHostModuleLifecycleTests` 断言 PreConfigure 拓扑顺序、null 参数和阶段边界。 |

### `ModuleServiceCollection`

| Field | Contract |
| --- | --- |
| Type | `ModuleServiceCollection` |
| Namespace | `AtomUI.City.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 在 module service configuration 阶段暴露受控 service registration collection。 |
| Owner | Module registry |
| Created By | `ServiceConfigurationContext`。 |
| Lifetime | 只在 module service configuration 阶段有效；阶段结束后由 module registry 冻结。 |
| DI Lifetime | 不进入 DI；包装 Host build 阶段的 `IServiceCollection`。 |
| Thread Safety | 与底层 `IServiceCollection` 一致，不保证并发写安全。 |
| Disposal | 无资源；Host Build 结束后 module 不应继续持有。 |
| Nullability | service descriptor 不得为空。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 调用临时 provider 创建入口必须失败；阶段冻结后所有 mutating API 抛 `InvalidOperationException`。 |
| Diagnostics | 触发 guard 时由 module lifecycle 记录 `AUCHOST106`。 |
| Plugin Boundary | 插件 module 只能通过该 collection 注册受控服务，不能写 Host runtime provider。 |
| AOT / Trimming | 不执行扫描，不依赖 dynamic code。 |
| Breaking Change Rules | 修改临时 provider guard、注册转发语义或生命周期属于 breaking change。 |
| Tests | `ApplicationHostModuleLifecycleTests` 断言禁止 `BuildServiceProvider`、冻结后拒绝 mutation 并记录诊断。 |

### `ModuleServiceCollectionBuildGuardExtensions`

| Field | Contract |
| --- | --- |
| Type | `ModuleServiceCollectionBuildGuardExtensions` |
| Namespace | `AtomUI.City.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 用更具体的 extension method 阻止 module service configuration 阶段创建临时 provider。 |
| Owner | Module registry |
| Created By | 静态扩展类型。 |
| Lifetime | 编译期 API。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 无状态。 |
| Disposal | 无释放。 |
| Nullability | services 为 null 时抛 `ArgumentNullException`。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | `BuildServiceProvider(ModuleServiceCollection)` 始终抛 `InvalidOperationException`。 |
| Diagnostics | module registry 捕获异常后写 `AUCHOST106`。 |
| Plugin Boundary | 插件 module 同样受 guard 约束。 |
| AOT / Trimming | 静态扩展方法，不依赖 reflection。 |
| Breaking Change Rules | 放宽或移除 guard 属于 breaking change。 |
| Tests | `ApplicationHostModuleLifecycleTests` 断言 guard message 和 diagnostics context。 |

### `DependsOnAttribute`

| Field | Contract |
| --- | --- |
| Type | `DependsOnAttribute` |
| Namespace | `AtomUI.City.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 声明模块依赖，用于 graph build 和 generator metadata。 |
| Owner | Module type declaration |
| Created By | 应用或插件模块源码。 |
| Lifetime | 编译期 metadata 和运行期 descriptor 输入。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | Attribute metadata 只读。 |
| Disposal | 无释放。 |
| Nullability | dependency type 不得为空。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 依赖类型不是 module、重复依赖、循环依赖进入 graph diagnostic。 |
| Diagnostics | 诊断包含 source module、dependency module 和 cycle path。 |
| Plugin Boundary | 插件不能依赖未暴露的 Host 私有 module 类型。 |
| AOT / Trimming | Attribute 必须可被 source generator 读取。 |
| Breaking Change Rules | 修改构造参数、allow multiple 规则或继承语义属于 breaking change。 |
| Tests | `ModuleAttributeTests` 断言 attribute metadata 和无效依赖。 |

### `ServiceAttribute`

| Field | Contract |
| --- | --- |
| Type | `ServiceAttribute` |
| Namespace | `AtomUI.City.DependencyInjection` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-005 |
| Purpose | 声明 AOT 友好的服务注册 metadata。 |
| Owner | Service type declaration |
| Created By | 应用、模块或插件源码。 |
| Lifetime | 编译期 metadata 和 generated manifest 输入。 |
| DI Lifetime | 由 attribute 或 marker 决定。 |
| Thread Safety | Attribute metadata 只读。 |
| Disposal | 无释放；服务实例由 DI container 释放。 |
| Nullability | exposed service type、lifetime metadata 不得非法。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | lifetime 冲突、exposed type 不可赋值或重复注册策略冲突产生 diagnostic。 |
| Diagnostics | 诊断包含 service type、exposed type、lifetime。 |
| Plugin Boundary | 插件服务注册只能进入插件 service scope 或受控 contribution。 |
| AOT / Trimming | 必须可由 generator 读取，不依赖运行时程序集扫描。 |
| Breaking Change Rules | 修改默认 lifetime、expose 规则或 replace/try-add 语义属于 breaking change。 |
| Tests | `ServiceRegistrationAttributeTests` 断言 lifetime、exposed services 和冲突诊断。 |

### `ScopedServiceAttribute`

| Field | Contract |
| --- | --- |
| Type | `ScopedServiceAttribute` |
| Namespace | `AtomUI.City.DependencyInjection` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-005 |
| Purpose | 简化 scoped service 声明。 |
| Owner | Service type declaration |
| Created By | 应用、模块或插件源码。 |
| Lifetime | 编译期 metadata。 |
| DI Lifetime | Scoped。 |
| Thread Safety | Attribute metadata 只读；实际服务线程安全由服务自身声明。 |
| Disposal | 无释放。 |
| Nullability | exposed service types 不得为空。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 与其他 lifetime marker 冲突产生 diagnostic。 |
| Diagnostics | 诊断包含 service type 和冲突 lifetime。 |
| Plugin Boundary | 插件 scoped service 进入插件 service scope。 |
| AOT / Trimming | 可被 generator 读取。 |
| Breaking Change Rules | 修改 scoped 默认语义属于 breaking change。 |
| Tests | `ServiceRegistrationAttributeTests` 断言 scoped lifetime。 |

### `ExposeServicesAttribute`

| Field | Contract |
| --- | --- |
| Type | `ExposeServicesAttribute` |
| Namespace | `AtomUI.City.DependencyInjection` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-005 |
| Purpose | 声明服务实现对外暴露的 service contract。 |
| Owner | Service type declaration |
| Created By | 应用、模块或插件源码。 |
| Lifetime | 编译期 metadata。 |
| DI Lifetime | 跟随实际 service registration。 |
| Thread Safety | Attribute metadata 只读。 |
| Disposal | 无释放。 |
| Nullability | exposed type collection 不得包含 null。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | exposed type 非 assignable 或重复冲突产生 diagnostic。 |
| Diagnostics | 诊断包含 implementation type 和 exposed service type。 |
| Plugin Boundary | 插件不能把插件私有类型暴露给 Host 长期持有。 |
| AOT / Trimming | generator 必须能生成强类型注册。 |
| Breaking Change Rules | 修改暴露默认值或 type matching 规则属于 breaking change。 |
| Tests | `ServiceRegistrationAttributeTests` 断言 exposed service 列表和非法暴露。 |

### `IHostDiagnostics`

| Field | Contract |
| --- | --- |
| Type | `IHostDiagnostics` |
| Namespace | `AtomUI.City.Diagnostics` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-006 |
| Purpose | 接收 Host build/start/stop/failure 诊断记录。 |
| Owner | Host diagnostics service |
| Created By | DI registration。 |
| Lifetime | Host 生命周期内有效。 |
| DI Lifetime | Singleton，测试可替换。 |
| Thread Safety | 必须支持多阶段并发记录或由实现显式串行化。 |
| Disposal | Host Dispose 后不接受新写入；已有 immutable record 可读取。 |
| Nullability | record 不得为空。 |
| Cancellation | 记录诊断不接受取消；不能阻塞清理。 |
| Failure Behavior | diagnostics collector 失败不得中断 Host 清理。 |
| Diagnostics | 自身失败写入 fallback sink 或测试 record。 |
| Plugin Boundary | 插件诊断必须带 pluginId，不能绕过 Host diagnostics。 |
| AOT / Trimming | record 使用稳定字段，不依赖动态序列化 contract。 |
| Breaking Change Rules | 修改 record 字段、severity 或 code 语义属于 breaking change。 |
| Tests | `HostDiagnosticsTests` 断言 code、severity、context 和 collector failure。 |

### `HostDiagnosticRecord`

| Field | Contract |
| --- | --- |
| Type | `HostDiagnosticRecord` |
| Namespace | `AtomUI.City.Diagnostics` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-006 |
| Purpose | 表示单条 Host 诊断。 |
| Owner | Diagnostics collector |
| Created By | Host、Lifecycle、ModuleSystem。 |
| Lifetime | immutable record；可 snapshot。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 不可变，可并发读取。 |
| Disposal | 无释放。 |
| Nullability | code、message、severity 不得为空或未知；context 可为空集合。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 非法 code 或 severity 创建失败。 |
| Diagnostics | record 本身是诊断输出。 |
| Plugin Boundary | 插件相关 record 必须包含 pluginId。 |
| AOT / Trimming | context key 使用字符串常量，避免依赖 runtime type name 作为唯一 contract。 |
| Breaking Change Rules | 删除字段、改变 code 含义或 context key 含义属于 breaking change。 |
| Tests | `HostDiagnosticsTests` 断言 immutable、snapshot 和 context 字段。 |

### `HostDiagnosticIds`

| Field | Contract |
| --- | --- |
| Type | `HostDiagnosticIds` |
| Namespace | `AtomUI.City.Diagnostics` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-006 |
| Purpose | 定义 Host 诊断码常量。 |
| Owner | Core diagnostics contract |
| Created By | Source code constant。 |
| Lifetime | 包版本生命周期内稳定。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 常量只读。 |
| Disposal | 无释放。 |
| Nullability | 诊断码常量不得为空。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 诊断码不能复用；废弃必须保留迁移说明。 |
| Diagnostics | 当前稳定码：`AUCHOST001`、`AUCHOST002`、`AUCHOST003`；Phase 1 目标失败码：`AUCHOST101` 到 `AUCHOST107`。 |
| Plugin Boundary | plugin 相关 Host 诊断使用独立 code 或 context，不复用普通 Host code。 |
| AOT / Trimming | 常量不依赖运行时生成。 |
| Breaking Change Rules | 删除、复用或改变诊断码语义属于 breaking change。 |
| Tests | `HostDiagnosticsTests` 断言当前代码存在且语义稳定。 |

### `IUiDispatcher`

| Field | Contract |
| --- | --- |
| Type | `IUiDispatcher` |
| Namespace | `AtomUI.City.Threading` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-007 |
| Purpose | 为 Presentation 提供 UI 调度抽象，不绑定 Avalonia。 |
| Owner | Presentation runtime 或 Testing fake dispatcher |
| Created By | Core 默认注册 unavailable 实现；Presentation 替换真实实现。 |
| Lifetime | Host 生命周期内有效。 |
| DI Lifetime | Singleton 或 platform-specific dispatcher lifetime。 |
| Thread Safety | 后台线程可调用调度 API；实现负责串行化 UI work。 |
| Disposal | Host Stop 后拒绝新 work；已排队 work 按实现策略取消或 drain。 |
| Nullability | work delegate 不得为空。 |
| Cancellation | 取消后不得执行尚未开始的 UI work。 |
| Failure Behavior | dispatcher unavailable、work exception 或 wrong thread 返回失败 Result 或声明异常。 |
| Diagnostics | 失败诊断包含 dispatcher type、thread id、operation id。 |
| Plugin Boundary | 插件 UI work 必须绑定 plugin owner，卸载时取消未完成 work。 |
| AOT / Trimming | Core 接口不引用 Avalonia/AtomUI 类型。 |
| Breaking Change Rules | 修改调度、取消、异常或线程识别语义属于 breaking change。 |
| Tests | `UiDispatcherIntegrationTests` 断言 Core 无 UI 引用和 unavailable 行为。 |

### `UnavailableUiDispatcher`

| Field | Contract |
| --- | --- |
| Type | `UnavailableUiDispatcher` |
| Namespace | `AtomUI.City.Threading` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-007 |
| Purpose | Core 默认 UI dispatcher，占位表达 UI 平台未接入。 |
| Owner | Core DI default |
| Created By | Core service registration。 |
| Lifetime | Host 生命周期内有效。 |
| DI Lifetime | Singleton。 |
| Thread Safety | 无内部 mutable UI state，可并发调用并稳定失败。 |
| Disposal | 无资源；Dispose 后仍不得执行 UI work。 |
| Nullability | work delegate 为 null 时优先参数异常。 |
| Cancellation | 已取消 token 返回取消结果，不执行 work。 |
| Failure Behavior | 未取消时返回 dispatcher unavailable 失败，不触碰 UI。 |
| Diagnostics | 诊断包含 dispatcher unavailable code 和 operation id。 |
| Plugin Boundary | 插件不能依赖 unavailable dispatcher 执行 UI work。 |
| AOT / Trimming | 不引用 UI 平台。 |
| Breaking Change Rules | 把默认行为改为静默成功或隐式线程切换属于 breaking change。 |
| Tests | `UiDispatcherIntegrationTests` 断言失败结果、取消和无 Avalonia 引用。 |

## 关键方法合同

### `ApplicationHost.CreateBuilder`

```text
Method: ApplicationHost.CreateBuilder
Feature: AUC-CORE-001
Purpose: 创建独立 Host builder。
Parameters: args 可为空，必须 defensive copy。
Return: 新的 IApplicationHostBuilder。
Nullability: args 可以为 null；返回值不能为空。
Cancellation: 无。
Exceptions or Result: 未来 options 非法时抛 ArgumentException。
Idempotency: 每次调用返回独立 builder。
Concurrency: 静态方法没有共享可变状态。
Side Effects: 读取 generic host 默认值和命令行配置。
Diagnostics: Build 前不写诊断。
Tests: ApplicationHostBuilderTests。
```

### `ApplicationHostBuilder.ConfigureServices`

```text
Method: ApplicationHostBuilder.ConfigureServices
Feature: AUC-CORE-001
Purpose: 在 Build 前注册服务。
Parameters: configureServices 不能为 null。
Return: 当前 builder。
Nullability: delegate 不能为 null。
Cancellation: 无。
Exceptions or Result: null delegate 抛 ArgumentNullException；Build 后调用抛 InvalidOperationException。
Idempotency: Build 前多次调用按顺序追加注册。
Concurrency: 配置阶段不保证线程安全。
Side Effects: 只修改 Build 前 service collection。
Diagnostics: 如果注册导致 Build 失败，失败诊断包含注册阶段。
Tests: ApplicationHostBuilderTests。
```

### `ApplicationHostBuilder.ConfigureHost`

```text
Method: ApplicationHostBuilder.ConfigureHost
Feature: AUC-CORE-001
Purpose: 在 Build 前注册 host options 修改。
Parameters: configureOptions 不能为 null。
Return: 当前 builder。
Nullability: delegate 不能为 null。
Cancellation: 无。
Exceptions or Result: null delegate 抛 ArgumentNullException；Build 后调用抛 InvalidOperationException。
Idempotency: Build 前多次调用按注册顺序执行。
Concurrency: 配置阶段不保证线程安全。
Side Effects: 只修改 pending host options。
Diagnostics: Build 失败诊断包含 options 校验上下文。
Tests: ApplicationHostBuilderTests; ApplicationHostOptionsTests。
```

### `ApplicationHostBuilder.Build`

```text
Method: ApplicationHostBuilder.Build
Feature: AUC-CORE-001
Purpose: 冻结 builder，配置 modules，构建 root provider，并创建 Host。
Parameters: 无。
Return: 新的 IApplicationHost。
Nullability: 返回 Host 不能为空。
Cancellation: 无。
Exceptions or Result: 重复 Build 或 module graph 非法抛 InvalidOperationException；module 配置异常写入 diagnostics 后重新抛出。
Idempotency: Build 只允许成功一次，后续调用必须失败。
Concurrency: Build 必须外部串行。
Side Effects: 冻结 Services、Configuration、Properties 和注册方法；成功时写 HostBuilt。
Diagnostics: 成功写 AUCHOST001；失败写 AUCHOST101。
Tests: ApplicationHostBuilderTests; HostDiagnosticsTests。
```

### `IApplicationHost.StartAsync`

```text
Method: IApplicationHost.StartAsync
Feature: AUC-CORE-002
Purpose: 启动 generic host，创建 application scope，配置 contributions，并初始化 modules。
Parameters: cancellationToken 控制启动等待和 module 初始化。
Return: Host 进入 Running 后完成的 Task。
Nullability: 返回 task 不能为空。
Cancellation: 完成前取消时 Host 必须保持稳定且可释放。
Exceptions or Result: Dispose 后抛 ObjectDisposedException；Stop 后再启动抛 InvalidOperationException；启动失败写 diagnostics 后重新抛出。
Idempotency: Running 状态重复调用不重新执行启动流程。
Concurrency: 并发调用由 Host state lock 串行化。
Side Effects: 创建 ApplicationScope，并且只执行一次 module initialization。
Diagnostics: 成功写 AUCHOST002；失败写 AUCHOST102。
Tests: ApplicationHostRuntimeTests; ApplicationHostLifecycleIntegrationTests。
```

### `IApplicationHost.StopAsync`

```text
Method: IApplicationHost.StopAsync
Feature: AUC-CORE-002
Purpose: 关闭 modules，停止 application 和 host scopes，并停止 generic host。
Parameters: cancellationToken 控制等待，但不能跳过已经开始的最小清理。
Return: Host 停止后完成的 Task。
Nullability: 返回 task 不能为空。
Cancellation: 取消只影响可取消等待；清理失败必须写 diagnostics。
Exceptions or Result: Dispose 后抛 ObjectDisposedException；shutdown 失败聚合并写 diagnostics。
Idempotency: Stopped 后重复调用不重新执行 shutdown。
Concurrency: 并发调用由同一个停止事务处理。
Side Effects: 取消 application 和 host scopes，成功时写 HostStopped。
Diagnostics: 成功写 AUCHOST003；失败写 AUCHOST103。
Tests: ApplicationHostRuntimeTests; ApplicationHostLifecycleIntegrationTests。
```

### `LifecycleScope.CreateChild`

```text
Method: LifecycleScope.CreateChild
Feature: AUC-CORE-003
Purpose: 创建链接父级 cancellation 的 child scope。
Parameters: kind 和 id 标识 child scope。
Return: 新的 LifecycleScope。
Nullability: id 不能为 null 或空白。
Cancellation: child token 链接 parent token。
Exceptions or Result: parent 处于 stopping、stopped、disposing 或 disposed 时抛 InvalidOperationException。
Idempotency: parent running 时每次调用创建一个 child。
Concurrency: child 创建由 scope lock 保护。
Side Effects: 把 child 加入 parent 的 Children 快照来源。
Diagnostics: Host-owned cleanup 报告非法 scope mutation 时写 AUCHOST104。
Tests: LifecycleScopeTreeTests。
```

### `LifecycleScope.StopAsync`

```text
Method: LifecycleScope.StopAsync
Feature: AUC-CORE-003
Purpose: 取消当前 scope，并 leaf-first 停止 child scopes。
Parameters: 无。
Return: scope 停止后完成的 ValueTask。
Nullability: 返回 ValueTask 有效。
Cancellation: Stop 会取消 scope token；不接受外部 cancellation。
Exceptions or Result: 重复 Stop 是 no-op；child failure 在挂接 diagnostics sink 时写 diagnostics。
Idempotency: 重复调用不改变顺序，也不重复取消。
Concurrency: 并发调用由 scope lock 串行化。
Side Effects: State 从 Running 变为 Stopping，再变为 Stopped。
Diagnostics: child stop 或 dispose 失败写 AUCHOST104。
Tests: LifecycleScopeTreeTests。
```

### `ModuleServiceCollectionBuildGuardExtensions.BuildServiceProvider`

```text
Method: ModuleServiceCollectionBuildGuardExtensions.BuildServiceProvider
Feature: AUC-CORE-004
Purpose: 阻止 module service configuration 阶段创建临时 ServiceProvider。
Parameters: services 不能为 null。
Return: 无成功返回。
Nullability: services 为 null 时抛 ArgumentNullException。
Cancellation: 无。
Exceptions or Result: 始终抛 InvalidOperationException，错误消息必须说明禁止创建 temporary service provider。
Idempotency: 每次调用都失败，不产生副作用。
Concurrency: 无共享状态。
Side Effects: 不构建 provider，不解析任何服务。
Diagnostics: module registry 捕获异常后写 AUCHOST106，context 包含 moduleType 和 stage。
Tests: ApplicationHostModuleLifecycleTests。
```

### `ServiceConfigurationContext.PreConfigure`

```text
Method: ServiceConfigurationContext.PreConfigure<TOptions>
Feature: AUC-CORE-004
Purpose: 注册早期 options 默认值或模块约定。
Parameters: configure 不能为 null。
Return: void。
Nullability: configure 为 null 时抛 ArgumentNullException。
Cancellation: 无；action 必须是同步、短执行、无 IO 的配置动作。
Exceptions or Result: action 本身不在注册时执行；执行异常由 ExecutePreConfigure 调用所在模块阶段承接。
Idempotency: 每次调用追加一个 action，不去重。
Concurrency: module registry 串行调用；不保证并发写安全。
Side Effects: 只写当前 ServiceConfigurationContext 的 preconfigure store。
Diagnostics: action 执行异常由 module registry 记录 module lifecycle failure。
Tests: ApplicationHostModuleLifecycleTests。
```

### `ServiceConfigurationContext.ExecutePreConfigure`

```text
Method: ServiceConfigurationContext.ExecutePreConfigure<TOptions>
Feature: AUC-CORE-004
Purpose: 对指定 options instance 按注册顺序执行早期配置动作。
Parameters: options 不能为 null。
Return: void。
Nullability: options 为 null 时抛 ArgumentNullException。
Cancellation: 无；调用者所在 async hook 必须在进入阶段前观察 token。
Exceptions or Result: action 抛出的异常原样向上抛出，并由 module registry 记录当前阶段诊断。
Idempotency: 每次调用都会重新执行当前 TOptions 的全部 action。
Concurrency: 不保证并发执行安全；由模块配置阶段串行调用。
Side Effects: 修改传入的 options instance，不创建 ServiceProvider。
Diagnostics: action 失败诊断包含 moduleType 和 stage。
Tests: ApplicationHostModuleLifecycleTests。
```

### `ModuleServiceCollection` mutating APIs

```text
Method: ModuleServiceCollection.Add/Clear/Insert/Remove/RemoveAt/index setter
Feature: AUC-CORE-004
Purpose: 在模块服务配置阶段转发受控服务注册，并在阶段结束后拒绝继续修改。
Parameters: service descriptor 或 index 遵守 IServiceCollection 原始规则。
Return: 与 IServiceCollection 原始 mutating API 一致。
Nullability: service descriptor 为 null 时遵守底层 collection 或 DI 扩展方法的参数异常。
Cancellation: 无。
Exceptions or Result: 服务配置阶段结束后抛 InvalidOperationException。
Idempotency: Freeze 后重复 mutating 调用稳定失败。
Concurrency: 不保证并发写安全。
Side Effects: 冻结前修改底层 IServiceCollection；冻结后无副作用。
Diagnostics: module 持有并越阶段修改时由调用方测试或上层诊断承接。
Tests: ApplicationHostModuleLifecycleTests。
```

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `ExposeServicesAttribute` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IScopedDependency` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ISingletonDependency` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ITransientDependency` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ScopedServiceAttribute` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ServiceAttribute` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `HostDiagnosticIds` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `HostDiagnosticRecord` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `HostDiagnosticSeverity` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IHostDiagnostics` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryHostDiagnostics` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationHost` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationHostBuilder` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationHostOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IApplicationContext` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IApplicationHost` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IApplicationHostBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecyclePipeline` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecyclePipelineBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleScope` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleScopeKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleScopeState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleStage` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleStageArea` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleStages` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationHostBuilderModularityExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationInitializationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationShutdownContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ContributionConfigurationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DependsOnAttribute` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IModule` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IModuleRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleBase` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleDependencyDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleDescriptor` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleOrigin` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleServiceCollection` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleServiceCollectionBuildGuardExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ServiceConfigurationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IUiDispatcher` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `UnavailableUiDispatcher` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
