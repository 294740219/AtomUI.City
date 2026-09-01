# AtomUI.City.Core Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 两轮 Core 门禁

第一轮执行 Unit、Contract 和 RuntimeLifecycle 测试，排除进程夹具：

```text
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --filter "FullyQualifiedName!~CoreHeadlessProcessTests"
```

第二轮执行无 UI Headless Console 进程测试。测试通过 `dotnet AtomUI.City.Core.HeadlessApp.dll --test-scenario <scenario>` 启动真实子进程，不加载 Avalonia 或 AtomUI：

```text
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --no-build --filter "FullyQualifiedName~CoreHeadlessProcessTests"
```

Headless 必须覆盖正常生命周期、启动失败补偿、关闭失败聚合、lifecycle middleware 诊断字段与事务关联、RunAsync cancellation、并发/递归停止以及模块后用户服务覆盖场景。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| Core 不允许引用 Avalonia、AtomUI、Roslyn、CLI、Templates 或 Testing 生产程序集。 | 至少一个明确测试断言，不能只断言流程成功。 |
| ApplicationHostBuilder 必须延迟执行用户服务配置，并在 Build 成功或失败后冻结捕获的服务集合。 | 至少一个明确测试断言，不能只断言流程成功。 |
| LifecyclePipeline stage 顺序必须稳定，同一 stage 内 middleware 顺序必须稳定。 | 至少一个明确测试断言，不能只断言流程成功。 |
| StartAsync、StopAsync、DisposeAsync 和 Dispose 必须有明确幂等规则，Stopped 后再次 Start 必须失败。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 模块配置阶段禁止 BuildServiceProvider 和运行期服务解析。 | 至少一个明确测试断言，不能只断言流程成功。 |
| IUiDispatcher 只定义抽象，Core 不提交真实 UI work。 | 至少一个明确测试断言，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-CORE-001 | RuntimeLifecycle + Headless | ApplicationHostBuilderTests; ApplicationHostOptionsTests; ApplicationHostRuntimeTests; CoreHeadlessProcessTests | 断言模块后用户服务配置、覆盖顺序、Build 冻结、应用身份必填、Context 不可变、Local AppDataPath、Dispose 后读取和真实进程启停。 | Options/UserServices 失败诊断；非法目录名；模块失败跳过用户 delegate；Run cancellation 后完整清理。 | Verified |
| AUC-CORE-002 | RuntimeLifecycle + Headless | LifecycleMiddlewarePipelineTests; ApplicationHostIndustrialLifecycleTests; CoreHeadlessProcessTests | 断言 stage 顺序、Host 管线接入、operationId 传播、准确 middleware 归因、并发事务共享、内部重入拒绝和 cleanup terminal 保证执行。 | middleware 异常、terminal 异常、诊断 sink 失败、递归调用、取消和超时不跳过最小清理。 | Verified |
| AUC-CORE-003 | RuntimeLifecycle | LifecycleScopeTreeTests | 断言 leaf-first、并发事务合并、锁外 cancellation callback、重入拒绝、故障隔离和诊断。 | child/cancellation callback 失败后继续清理其他 child。 | Verified |
| AUC-CORE-004 | Unit + RuntimeLifecycle + Headless | ModuleBaseTests; ModuleDescriptorTests; ApplicationHostModuleLifecycleTests; ApplicationHostIndustrialLifecycleTests | 断言依赖排序、阶段顺序、Application scoped provider 和逆序关闭。 | Build 失败释放实例；部分初始化回滚；关闭失败聚合。 | Verified |
| AUC-CORE-005 | RuntimeLifecycle | ServiceRegistrationAttributeTests | 断言 lifetime、exposed services、AOT metadata 可读。 | 冲突 lifetime 或服务类型无效必须失败或诊断。 | Verified |
| AUC-CORE-006 | RuntimeLifecycle + Headless | HostDiagnosticsTests; ApplicationHostIndustrialLifecycleTests; CoreHeadlessProcessTests | 断言 AUCHOST001-108、Build failure 读取、有界顺序、dropped count、middleware 关键字段和 Host 摘要 operationId 关联。 | 诊断 collector 不可用不能中断 Host；正常 cancellation 不写 middleware failure。 | Verified |
| AUC-CORE-007 | RuntimeLifecycle | UiDispatcherIntegrationTests | 断言不可用 dispatcher 返回失败且 Core 不引用 Avalonia。 | 默认 dispatcher 不可用；Presentation 必须替换真实实现。 | Verified |
| AUC-CORE-008 | Generator + RuntimeLifecycle + Headless + NativeAOT | GeneratedModuleCatalogTests; AtomUICityIncrementalGeneratorModularityTests; CoreHeadlessProcessTests | 断言自动根和显式根汇入同一 root set、生成 Catalog 只激活依赖闭包、Descriptor/Factory 无模块类型扫描；原生进程执行 generated-modules。 | 多默认根、Library 默认根、非模块默认根、非法 factory、registrar 冲突、缺失依赖、循环依赖和 generated registrar metadata 缺失。 | Verified |

AUC-CORE-008 NativeAOT 门禁：

```powershell
dotnet restore fixtures/AtomUI.City.Core.HeadlessApp/AtomUI.City.Core.HeadlessApp.csproj -r win-x64 -p:AtomUICityHeadlessPublishAot=true
dotnet publish fixtures/AtomUI.City.Core.HeadlessApp/AtomUI.City.Core.HeadlessApp.csproj -c Release -r win-x64 -p:AtomUICityHeadlessPublishAot=true --no-restore
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario generated-modules
```

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
