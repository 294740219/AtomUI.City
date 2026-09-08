# AtomUI.City.Routing Journal and Reuse

## Journal Entry

内部 entry 只保存 route id、只读 string parameters、graph version、contribution id、reuse key 和安全标量 resolved data。它不保存 descriptor、ViewModel、View、ServiceProvider、delegate、ActivationScope 或插件私有对象。

## Behavior

- Push：旧 current 进入 back，清空 forward。
- Replace/ReplaceCurrent：替换 current，清空 forward。
- Reset：清空 back/forward，设置 current。
- Skip：提交 snapshot 但不改 journal。
- Back/Forward 的取 entry、导航和栈移动处于同一 scope gate。
- 失败时 entry 放回原栈。
- entry 的 route id 在当前 graph 已不存在而导致 NotFound 时，自动丢弃 stale entry 并继续查找下一项；即使同一个 contribution id 后续被重新使用，也不能令旧 route entry 复活。
- Back/Forward 发生 redirect 时，新的 current entry 记录最终提交 route，而不是已经过时的原始 entry。
- capacity 从最旧 back entry 开始裁剪，不移除 current。

内部 restore marker 只在该 entry 的全部 resolver data 都是上述安全标量时恢复数据并跳过 resolver。只要存在任意 DTO、插件私有对象或其他非标量值，entry 不保存该对象，返回 route 时重新运行完整 resolver hierarchy，不能用残缺数据冒充完整恢复。ViewModel 实例复用和 KeepAlive cache 明确属于 Presentation/MVVM，不是 Routing 1.0 能力。
