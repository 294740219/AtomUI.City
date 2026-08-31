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

- `ConfigureLifecycle(Action<LifecyclePipelineBuilder>)`：注册 Host lifecycle middleware。
- `GetBuildDiagnostics()`：Build 抛异常后读取结构化诊断。
- `ApplicationHostOptions.DiagnosticsCapacity`：控制默认 Host 诊断容量，默认 1024。
- `InMemoryHostDiagnostics(int capacity)`、`Capacity`、`DroppedCount`：提供有界内存诊断。
- `LifecycleScope.CreateRoot(..., IHostDiagnostics)`：为独立 Scope tree 接入清理诊断。
- `ModuleServiceCollectionBuildGuardExtensions.BuildServiceProvider(..., bool/ServiceProviderOptions)`：补齐常用临时 Provider 入口的稳定失败行为。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
