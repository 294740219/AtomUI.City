# AtomUI.City.Routing Implementation Plan

本文件跟踪模块 Feature ID 到现有实现基线、产品级测试缺口和后续实现工作的关系。

| Feature ID | Documented | API Contract | Existing Test File | Implementation Baseline | Product Contract Tests | Required Assertions | Implementation Gap | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AUC-ROUTING-001 | Yes | Yes | RouteTemplateTests; RouteDefinitionAttributeTests | Baseline Exists | Required | 断言合法模板、非法模板、参数边界、属性默认值和稳定排序。 | Required | Ready to Start Product Implementation |
| AUC-ROUTING-002 | Yes | Yes | RouteGraphAndMatcherTests | Baseline Exists | Required | 断言 graph 不可变、冲突拒绝、plugin route revoke 后旧 snapshot 仍只读可用。 | Required | Ready to Start Product Implementation |
| AUC-ROUTING-003 | Yes | Yes | RouteGraphAndMatcherTests; RoutingParameterBoundaryTests | Baseline Exists | Required | 断言优先级、参数转换、constraint、并发匹配和非法输入。 | Required | Ready to Start Product Implementation |
| AUC-ROUTING-004 | Yes | Yes | NavigationScopeTests | Baseline Exists | Required | 断言失败不改变 current snapshot、取消不提交、重复 dispose 幂等、并发策略稳定。 | Required | Ready to Start Product Implementation |
| AUC-ROUTING-005 | Yes | Yes | RouteGuardTests | Baseline Exists | Required | 断言 enter/leave 顺序、deny、redirect、loop detection、异常映射和取消。 | Required | Ready to Start Product Implementation |
| AUC-ROUTING-006 | Yes | Yes | RouteGraphAndMatcherTests | Baseline Exists | Required | 断言 target descriptor 内容完整、Routing 不依赖 Presentation、失败不创建 ViewModel。 | Required | Ready to Start Product Implementation |
| AUC-ROUTING-007 | Yes | Yes | RouteGraphAndMatcherTests | Baseline Exists | Required | 断言插件贡献、冲突隔离、卸载撤销、旧 snapshot 只读。 | Required | Ready to Start Product Implementation |
| AUC-ROUTING-008 | Yes | Yes | NavigationScopeTests | Baseline Exists | Required | 断言 push/replace/back/forward、容量裁剪、失败不写历史和 reuse key。 | Required | Ready to Start Product Implementation |

## 更新规则

- `Baseline Exists` 只表示当前仓库已有实现或测试入口，不表示产品级完成。
- `Product Contract Tests` 为 `Required` 时，不能把 Feature 标记为 Implemented。
- 新增 public API、manifest、diagnostic、MSBuild property、CLI command 或 template variable 时，必须先更新标准文档页。
- 文档与代码不一致时，先修正文档并确认，再调整实现。
