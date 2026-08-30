namespace AtomUI.City.Core.Lifecycle;

public delegate ValueTask LifecycleMiddleware(LifecycleContext context, LifecycleNext next);
