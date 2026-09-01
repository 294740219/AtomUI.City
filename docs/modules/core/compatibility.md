# AtomUI.City.Core Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- Core 不允许引用 Avalonia、AtomUI、Roslyn、CLI、Templates 或 Testing 生产程序集。
- ApplicationHostBuilder Build 后必须冻结服务注册入口。
- LifecyclePipeline stage 顺序必须稳定，同一 stage 内 middleware 顺序必须稳定。
- StartAsync、StopAsync、DisposeAsync 和 Dispose 必须有明确幂等规则。
- 模块配置阶段禁止 BuildServiceProvider；常用入口由 runtime guard 拒绝，非测试 City 项目中的显式 Provider 创建和 Microsoft Generic Host 构建/启动入口由 `AUCANL0001` 阻止编译。运行期服务解析只能发生在 Provider 构建后的声明生命周期阶段。
- IUiDispatcher 只定义抽象，Core 不提交真实 UI work。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。

## 1.0 Preview 命名空间标准化

Core 在 1.0 冻结前完成一次显式 breaking migration，程序集不变，命名空间统一增加 `Core` 层级：

| 旧命名空间 | 新命名空间 |
| --- | --- |
| `AtomUI.City.Hosting` | `AtomUI.City.Core.Hosting` |
| `AtomUI.City.Lifecycle` | `AtomUI.City.Core.Lifecycle` |
| `AtomUI.City.Modularity` | `AtomUI.City.Core.Modularity` |
| `AtomUI.City.DependencyInjection` | `AtomUI.City.Core.DependencyInjection` |
| `AtomUI.City.Diagnostics` | `AtomUI.City.Core.Diagnostics` |
| `AtomUI.City.Threading` | `AtomUI.City.Core.Threading` |

仓库内生产模块、测试、模板、MSBuild contract 和 generator metadata 已同步迁移。1.0 发布后这些新命名空间进入常规兼容性承诺。

## 1.0 Preview 服务配置入口收口

`IApplicationHostBuilder.Services` 和 `ApplicationHostBuilder.Services` 在 1.0 冻结前移除。应用必须把直接集合修改迁移为延迟服务配置：

```csharp
// Before
builder.Services.AddSingleton<AppService>();

// After
builder.ConfigureServices(services => services.AddSingleton<AppService>());
```

`ConfigureServices` delegate 从调用时立即执行改为 Build 时在全部模块服务阶段之后执行。依赖 delegate 立即副作用或立即异常的代码必须迁移到显式的普通方法；服务注册失败现在由 `Build()` 抛出并写入 `UserServices` stage 诊断。

## 本轮新增 API

- `ApplicationHostOptions.ApplicationId`：必填的稳定应用产品标识。
- `ApplicationHostOptions.ApplicationVersion`：可选显式版本；默认从入口程序集解析。
- `IApplicationContext.ApplicationId`、`ApplicationInstanceId`、`ApplicationVersion`：补齐不可变应用实例身份。
- `ConfigureLifecycle(Action<LifecyclePipelineBuilder>)`：注册 Host lifecycle middleware。
- `GetBuildDiagnostics()`：Build 抛异常后读取结构化诊断。
- `ApplicationHostOptions.DiagnosticsCapacity`：控制默认 Host 诊断容量，默认 1024。
- `InMemoryHostDiagnostics(int capacity)`、`Capacity`、`DroppedCount`：提供有界内存诊断。
- `LifecycleScope.CreateRoot(..., IHostDiagnostics)`：为独立 Scope tree 接入清理诊断。
- `ModuleServiceCollectionBuildGuardExtensions.BuildServiceProvider(..., bool/ServiceProviderOptions)`：补齐常用临时 Provider 入口的稳定失败行为。
- `ApplicationModuleAttribute`：允许可执行应用项目声明唯一 generated default root，从而省略 `UseModule<AppModule>()`。
- `GeneratedModuleManifestAttribute`、`IModuleRegistrar`、`IModuleRegistrarContext`：连接编译期生成的模块 Catalog 与 Host Build；应用业务代码不直接调用。
- `LifecycleContext.OperationId`：增加只读 lifecycle transaction id；构造函数末尾的可选参数保持现有调用源码兼容。
- `LifecyclePipelineBuilder.Use<TMiddleware>(...)`：允许显式声明稳定 middleware 诊断类型；现有 delegate overload 保留并使用兼容推断。
- `HostDiagnosticIds.LifecycleMiddlewareFailed` (`AUCHOST108`)：定位 lifecycle middleware 的 stage、类型、operationId 和异常类型。

`UseModule<TModule>()` 保留为显式附加根和无生成清单时的兼容入口；它与 generated default root 合并并去重。Library 和 Plugin 中的 `[ApplicationModule]` 现在由 `AUCGEN008` 拒绝。

## Host 诊断码兼容合同

| Code | Name | 当前稳定语义 |
| --- | --- | --- |
| `AUCHOST001` | HostBuilt | Generic Host 与 Core runtime services 成功构建后写入 Info 记录。 |
| `AUCHOST002` | HostStarted | Host Start transaction 成功完成并进入 Running 后写入 Info 记录。 |
| `AUCHOST003` | HostStopped | Host Stop transaction 完成且状态发布为 Stopped 后写入 Info 记录；存在清理失败时仍先写本记录，再写 `AUCHOST103`。 |
| `AUCHOST101` | HostBuildFailed | Host Build 任一阶段失败时写入 Error 记录，context 至少定位 build stage 和 exception type。 |
| `AUCHOST102` | HostStartFailed | 非正常取消导致 Start transaction 失败时写入 Error 记录，context 包含 operation、operationId 和 exception type。 |
| `AUCHOST103` | HostStopFailed | Stop 清理聚合失败或启动 rollback 失败时写入 Error 记录；同一 transaction 可因多个 rollback failure 写入多条。 |
| `AUCHOST104` | LifecycleScopeCleanupFailed | LifecycleScope child 或 cleanup operation 失败时写入 Error 记录，并继续清理其他资源。 |
| `AUCHOST105` | ModuleGraphFailed | Build 的 ModuleGraph 阶段失败时与 `AUCHOST101` 一起写入 Error 记录。 |
| `AUCHOST106` | ModuleLifecycleFailed | 模块 lifecycle hook 失败时写入 Error 记录，context 定位 module id、module type、stage 和 exception type。 |
| `AUCHOST107` | DispatcherUnavailable | 默认 `UnavailableUiDispatcher` 被调用时以该 code 标识失败消息；当前不向 `IHostDiagnostics` 额外写 record。 |
| `AUCHOST108` | LifecycleMiddlewareFailed | 非正常取消的 lifecycle middleware 异常写入 Error 记录，强类型 Stage 及 middlewareType、operationId、exceptionType 为必需定位字段。 |

诊断码一经公开不得删除、复用或改变名称映射。修改触发条件、severity、错误/成功含义或删除必需 context 字段属于兼容性变更；message 文案可以优化，新增可选 context 字段属于向后兼容扩展。

## LifecycleScope 状态机兼容合同

`LifecycleScope` 当前稳定状态迁移为 `Running -> Stopping -> Stopped/Faulted -> Disposing -> Disposed`。Scope 创建完成后立即处于 `Running`；正常停止进入 `Stopped`，停止失败进入 `Faulted`，两者均可继续释放并最终进入 `Disposed`。

`LifecycleScopeState.Created`、`Starting`、`CancelRequested` 和 `UnloadPending` 是保留枚举值，当前 `LifecycleScope` 不会产生。保留值不构成当前可观察行为；未来开始产生任一保留状态属于状态机行为兼容性变更，必须更新 API 合同、迁移说明和测试后才能启用。

## 1.0 Preview ApplicationContext 收口

`IApplicationContext` 从可变运行时数据袋收口为不可变应用实例描述符。以下成员被移除：`Configuration`、`Services` 和 `Properties`；配置与服务分别迁移到 DI 和 `IApplicationHost.Services`。具体 `ApplicationContext` 类型不再公开或注册到 DI，模块上下文统一依赖 `IApplicationContext`。

`IApplicationHostBuilder.Properties` 和具体 Builder 的同名属性被移除。Core 构建诊断继续通过 `GetBuildDiagnostics()` 提供，内部状态不再暴露给应用开发者。

现有应用必须在 Build 前配置稳定 id：

```csharp
builder.ConfigureHost(options =>
{
    options.ApplicationId = "Company.Product";
    options.ApplicationName = "Product";
});
```

`AppDataPath` 的 Windows 默认基础目录由 Roaming `ApplicationData` 改为 `LocalApplicationData`，末级目录仍使用 `ApplicationName`。框架不自动创建该目录；需要旧目录数据的应用必须自行迁移。本轮不提供 Roaming 替代属性，跨平台语义化路径服务记录在 Hosting 的 deferred 专题中。

## 1.0 Preview 动态发现入口撤回

`ApplicationHostOptions.AllowDynamicDiscovery` 和 MSBuild 属性 `AtomUICityAllowDynamicDiscovery` 被移除。它们此前没有任何运行时或构建行为，不能作为兼容性合同保留。Generated Module Catalog 只进行编译期静态发现；运行时程序集扫描未来必须作为独立 Feature 同时交付扫描边界、AOT/trimming 诊断和测试后才能重新公开。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
