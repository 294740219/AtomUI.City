# AtomUI.City.Routing Plugins

## Routing 责任

- 接收 generated/application immutable descriptors，并在 `RouteContribution` 边界为无主 descriptor 创建带稳定 `ContributionId` 的副本；冲突所有权被拒绝。
- 验证 graph conflict、ExtensionPoint 和 behavior contracts。
- 原子发布或完整拒绝 candidate。
- 为 contribution 保存可选 service resolver。
- 返回可撤销、幂等且失败可重试的 lease。
- 成功撤销后新导航不可匹配 route，并释放 resolver 引用。
- Back/Forward 跳过已撤销 contribution entry。

## PluginSystem 责任

```text
load provider
-> create RouteContribution
-> add and own lease
-> on unload: stop admission / cancel work / leave active route
-> dispose route lease
-> dispose plugin provider and ALC
```

Routing 允许已开始事务持有旧 immutable graph，但 graph snapshot 不租赁 contribution resolver。因此 PluginSystem 必须先停止 admission 并 drain，再撤销 route lease 和释放插件 Provider/ALC。Routing 不定义插件状态机、超时或 force-close UI。

Contribution resolver 只能暴露 Host shared contracts。Resolver data 不应返回插件私有长期对象；Journal 会过滤非标量数据，但当前 active snapshot 在离开 route 前仍可持有 resolver result。
