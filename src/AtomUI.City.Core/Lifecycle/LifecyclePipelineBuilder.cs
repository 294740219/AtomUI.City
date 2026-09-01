namespace AtomUI.City.Core.Lifecycle;

public sealed class LifecyclePipelineBuilder
{
    private readonly List<LifecycleMiddlewareRegistration> _middleware = [];

    public LifecyclePipelineBuilder Use(LifecycleMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        return UseCore(stage: null, InferMiddlewareType(middleware), middleware);
    }

    public LifecyclePipelineBuilder Use(LifecycleStage stage, LifecycleMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        return UseCore(stage, InferMiddlewareType(middleware), middleware);
    }

    public LifecyclePipelineBuilder Use<TMiddleware>(LifecycleMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        return UseCore(stage: null, typeof(TMiddleware), middleware);
    }

    public LifecyclePipelineBuilder Use<TMiddleware>(
        LifecycleStage stage,
        LifecycleMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);

        return UseCore(stage, typeof(TMiddleware), middleware);
    }

    public LifecyclePipeline Build(Func<LifecycleContext, ValueTask> terminalHandler)
    {
        ArgumentNullException.ThrowIfNull(terminalHandler);

        return new LifecyclePipeline(_middleware.ToArray(), terminalHandler);
    }

    private LifecyclePipelineBuilder UseCore(
        LifecycleStage? stage,
        Type middlewareType,
        LifecycleMiddleware middleware)
    {
        _middleware.Add(new LifecycleMiddlewareRegistration(stage, middlewareType, middleware));

        return this;
    }

    private static Type InferMiddlewareType(LifecycleMiddleware middleware)
    {
        return middleware.Method.DeclaringType
               ?? middleware.Target?.GetType()
               ?? typeof(LifecycleMiddleware);
    }
}
