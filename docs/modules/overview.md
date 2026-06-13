# 模块文档

本目录维护 AtomUI.City 各框架包的模块级设计。模块文档是实现合同，不是介绍材料。所有模块必须按照 [模块文档设计规范](../engineering/module-documentation-standard.md) 维护。

## 模块开工规则

- 文档确认后才能开始写代码。
- 每个功能点必须有 Feature ID。
- 每个 Feature ID 必须有 API contract、失败行为、诊断规则和测试矩阵。
- 涉及生命周期、线程、插件、manifest、source generator、配置或兼容性的功能必须按高风险功能规格编写。
- 文档和实现不一致时，先修正文档并确认，再继续实现。

## Feature 状态语义

Feature 状态必须区分“可开工”和“产品级完成”。

| 状态 | 含义 |
| --- | --- |
| `Not Started` | 尚未完成设计或没有进入实现队列。 |
| `In Design` | 正在补齐文档合同，不能写代码。 |
| `Ready to Start Product Implementation` | 文档足以开始实现，但仍可能有 `implementation-plan.md` 中标记的实现和测试缺口。 |
| `In Implementation` | 正在实现或补测试。 |
| `Implemented` | 实现完成，但尚未通过完整产品门禁。 |
| `Verified` | 文档、实现、单元测试、诊断断言、生命周期/线程/插件专项测试和公共 API review 均通过。 |
| `Blocked` | 因设计冲突、测试不可行、依赖未确认或兼容性问题暂停。 |
| `Deprecated` | 已废弃，保留迁移和兼容说明。 |

`Ready to Start Product Implementation` 不能被解释为产品级完成。产品级完成只能使用 `Verified`。

## 模块索引

| 模块 | 等级 | 执行边界 | Overview | Features | Implementation Plan |
| --- | --- | --- | --- | --- | --- |
| Core | Level 3 | Host runtime kernel | [core/overview.md](core/overview.md) | [core/features.md](core/features.md) | [core/implementation-plan.md](core/implementation-plan.md) |
| PluginSystem | Level 3 | Host runtime plugin boundary | [plugins/overview.md](plugins/overview.md) | [plugins/features.md](plugins/features.md) | [plugins/implementation-plan.md](plugins/implementation-plan.md) |
| EventBus | Level 3 | Host runtime service | [eventbus/overview.md](eventbus/overview.md) | [eventbus/features.md](eventbus/features.md) | [eventbus/implementation-plan.md](eventbus/implementation-plan.md) |
| State | Level 3 | Host runtime state manager | [state/overview.md](state/overview.md) | [state/features.md](state/features.md) | [state/implementation-plan.md](state/implementation-plan.md) |
| Data | Level 3 | Host runtime data pipeline | [data/overview.md](data/overview.md) | [data/features.md](data/features.md) | [data/implementation-plan.md](data/implementation-plan.md) |
| Routing | Level 3 | Host runtime navigation graph | [routing/overview.md](routing/overview.md) | [routing/features.md](routing/features.md) | [routing/implementation-plan.md](routing/implementation-plan.md) |
| Presentation | Level 3 | Avalonia/AtomUI runtime bridge | [presentation/overview.md](presentation/overview.md) | [presentation/features.md](presentation/features.md) | [presentation/implementation-plan.md](presentation/implementation-plan.md) |
| Mvvm | Level 2 | ViewModel programming model | [mvvm/overview.md](mvvm/overview.md) | [mvvm/features.md](mvvm/features.md) | [mvvm/implementation-plan.md](mvvm/implementation-plan.md) |
| Localization | Level 3 | Host runtime localization service | [localization/overview.md](localization/overview.md) | [localization/features.md](localization/features.md) | [localization/implementation-plan.md](localization/implementation-plan.md) |
| Security | Level 2 | Host runtime security service | [security/overview.md](security/overview.md) | [security/features.md](security/features.md) | [security/implementation-plan.md](security/implementation-plan.md) |
| Build | Level 3 | MSBuild and repository engineering boundary | [build/overview.md](build/overview.md) | [build/features.md](build/features.md) | [build/implementation-plan.md](build/implementation-plan.md) |
| Generators | Level 3 | Roslyn compile-time analyzer boundary | [generators/overview.md](generators/overview.md) | [generators/features.md](generators/features.md) | [generators/implementation-plan.md](generators/implementation-plan.md) |
| Cli | Level 2 | Command process boundary | [cli/overview.md](cli/overview.md) | [cli/features.md](cli/features.md) | [cli/implementation-plan.md](cli/implementation-plan.md) |
| Templates | Level 2 | Template render and package boundary | [templates/overview.md](templates/overview.md) | [templates/features.md](templates/features.md) | [templates/implementation-plan.md](templates/implementation-plan.md) |
| Testing | Level 3 | Test-only framework boundary | [testing/overview.md](testing/overview.md) | [testing/features.md](testing/features.md) | [testing/implementation-plan.md](testing/implementation-plan.md) |

## 文档完成判断

模块文档合格，不是因为文件数量多，而是因为开发者能根据文档判断：模块边界、public API、生命周期、线程边界、插件卸载、失败表达、诊断、测试和兼容性。
