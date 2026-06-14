namespace AtomUI.City.Mvvm;

public sealed class InteractionContext<TRequest>
{
    public InteractionContext(TRequest request)
        : this(
            request,
            Guid.NewGuid(),
            activationScopeId: null,
            handlerType: null)
    {
    }

    internal InteractionContext(
        TRequest request,
        Guid requestId,
        Guid? activationScopeId,
        Type? handlerType)
    {
        Request = request;
        RequestId = requestId;
        ActivationScopeId = activationScopeId;
        HandlerType = handlerType;
    }

    public TRequest Request { get; }

    public Guid RequestId { get; }

    public Type RequestType => typeof(TRequest);

    public Guid? ActivationScopeId { get; }

    public Type? HandlerType { get; }
}
