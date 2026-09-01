namespace AtomUI.City.Core.Lifecycle;

internal sealed record LifecycleMiddlewareRegistration(
    LifecycleStage? Stage,
    Type MiddlewareType,
    LifecycleMiddleware Handler);
