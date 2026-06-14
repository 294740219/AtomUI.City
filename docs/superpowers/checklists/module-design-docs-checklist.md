# AtomUI.City 模块设计文档 Checklist

状态：第二轮按模块文档设计规范重写
维护规则：任何勾选、补充或调整都必须先取得用户同意。

## 0. 文档治理基线

- [x] 整体架构设计：`docs/architecture/overview.md`
- [x] 包边界：`docs/architecture/package-boundaries.md`
- [x] 编程范式：`docs/architecture/programming-model.md`
- [x] 生命周期架构：`docs/architecture/lifecycle.md`
- [x] 插件系统架构规范：`docs/architecture/plugin-system.md`
- [x] Source Generator 设计规范：`docs/architecture/source-generation.md`
- [x] 文档先行治理规范：`docs/engineering/documentation-governance.md`
- [x] 模块文档设计规范：`docs/engineering/module-documentation-standard.md`

## 1. 模块标准文档完成表

| 模块 | 等级 | Overview | Features |
| --- | --- | --- | --- |
| AtomUI.City.Core | Level 3 | `docs/modules/core/overview.md` | `docs/modules/core/features.md` |
| AtomUI.City.PluginSystem | Level 3 | `docs/modules/plugins/overview.md` | `docs/modules/plugins/features.md` |
| AtomUI.City.EventBus | Level 3 | `docs/modules/eventbus/overview.md` | `docs/modules/eventbus/features.md` |
| AtomUI.City.State | Level 3 | `docs/modules/state/overview.md` | `docs/modules/state/features.md` |
| AtomUI.City.Routing | Level 3 | `docs/modules/routing/overview.md` | `docs/modules/routing/features.md` |
| AtomUI.City.Presentation | Level 3 | `docs/modules/presentation/overview.md` | `docs/modules/presentation/features.md` |
| AtomUI.City.Data | Level 3 | `docs/modules/data/overview.md` | `docs/modules/data/features.md` |
| AtomUI.City.Localization | Level 3 | `docs/modules/localization/overview.md` | `docs/modules/localization/features.md` |
| AtomUI.City.Mvvm | Level 2 | `docs/modules/mvvm/overview.md` | `docs/modules/mvvm/features.md` |
| AtomUI.City.Security | Level 2 | `docs/modules/security/overview.md` | `docs/modules/security/features.md` |
| AtomUI.City.Build | Level 3 | `docs/modules/build/overview.md` | `docs/modules/build/features.md` |
| AtomUI.City.Generators | Level 3 | `docs/modules/generators/overview.md` | `docs/modules/generators/features.md` |
| AtomUI.City.Cli | Level 2 | `docs/modules/cli/overview.md` | `docs/modules/cli/features.md` |
| AtomUI.City.Templates | Level 2 | `docs/modules/templates/overview.md` | `docs/modules/templates/features.md` |
| AtomUI.City.Testing | Level 3 | `docs/modules/testing/overview.md` | `docs/modules/testing/features.md` |

## 2. 强制完成标准

- [x] 每个模块有 `overview.md`、`architecture.md`、`features.md`、`api-contracts.md`、`lifecycle.md`、`threading.md`、`diagnostics.md`、`testing.md`、`compatibility.md`、`integration.md`。
- [x] 1.0 发布进度只由 `docs/superpowers/plans/2026-06-11-development-tracking-plan.md` 维护。
- [x] 每个模块有 Feature ID。
- [x] 每个模块有测试矩阵。
- [x] 每个模块有诊断规则。
- [x] 每个模块有兼容性规则。
- [x] 每个模块有 Host、PluginSystem 和 Testing 集成说明。
- [x] 独立包 `AtomUI.City.Generators` 已纳入模块文档治理。
