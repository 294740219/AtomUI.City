# AtomUI.City.Routing Diagnostics and Testing

本文件不建立第二套诊断码或测试矩阵。

- 诊断唯一合同：[diagnostics.md](diagnostics.md)。
- 测试唯一矩阵：[testing.md](testing.md)。
- Feature 状态：[features.md](features.md)。

测试必须同时断言业务结果和可观测结果：错误码、operationId、route/component type、stage、graph version 或 contribution id。诊断 sink 故障隔离必须有直接测试。

Routing 测试不得伪造另一套 path matcher；`AtomUI.City.Testing.RoutingTestHost` 委托生产 `RouteGraphSnapshot`、`RouteMatcher` 和 `NavigationScope`。
