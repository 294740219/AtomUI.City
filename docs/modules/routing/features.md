# AtomUI.City.Routing Features

| Feature ID | Feature | Status | Primary evidence |
| --- | --- | --- | --- |
| AUC-ROUTING-001 | Route definition, template and generated references | Verified | RouteTemplateTests; RoutingFreezeAuditTests; RoutingFinalFreezeTests; generator routing tests |
| AUC-ROUTING-002 | Immutable graph build and validation | Verified | RouteGraphAndMatcherTests; RoutingFreezeAuditTests; RoutingFinalFreezeTests |
| AUC-ROUTING-003 | Matching, parameters, named outlets and deep links | Verified | RouteTemplateTests; NavigationAdvancedContractTests; RoutingAuditRegressionTests |
| AUC-ROUTING-004 | Navigation transaction and concurrency | Verified | NavigationScopeTests; RoutingAuditRegressionTests; RoutingFreezeAuditTests; RoutingFinalFreezeTests |
| AUC-ROUTING-005 | Guard, match policy and redirect pipeline | Verified | RouteGuardTests; RoutingAuditRegressionTests; RoutingFinalFreezeTests |
| AUC-ROUTING-006 | ViewModel target descriptor boundary | Verified | RouteGraphAndMatcherTests; RoutingAssemblyTests |
| AUC-ROUTING-007 | Registry, ExtensionPoint and contribution lease | Verified | RouteRegistryTests; RoutingAuditRegressionTests; RoutingFreezeAuditTests |
| AUC-ROUTING-008 | Journal, restore and reuse metadata | Verified | NavigationScopeTests; NavigationAdvancedContractTests; RoutingAuditRegressionTests; RoutingFreezeAuditTests; RoutingFinalFreezeTests |
| AUC-ROUTING-009 | Resolver transaction data | Verified | RouteResolverAndDiagnosticsTests; RoutingAuditRegressionTests |
| AUC-ROUTING-010 | Route middleware | Verified | RouteMiddlewareTests; RoutingAuditRegressionTests; RoutingFreezeAuditTests; RoutingFinalFreezeTests; generator routing tests |
| AUC-ROUTING-011 | Host DI and diagnostics | Verified | RouteRegistryTests; RouteResolverAndDiagnosticsTests; RoutingAuditRegressionTests |
| AUC-ROUTING-012 | Routing test host parity | Verified | RoutingTestHostTests |

## Acceptance Rules

A feature is Verified only when public API, runtime implementation, failure behavior, cancellation/concurrency behavior, documentation and direct tests agree. Generated capabilities must also compile the generated source.

## Explicit 1.0 Boundaries

- “Reuse” in Routing means stable `ReuseKey` and journal restoration metadata; ViewModel instance caches belong to Presentation/MVVM.
- “Transaction commit” means atomic `NavigationSnapshot` and journal publication; it does not include UI commit.
- “Plugin route support” means validated contribution, service boundary and revocation; PluginSystem owns drain/ALC unload.
- Custom constraints and custom constraint discovery are not AUC-ROUTING-001 in 1.0.
