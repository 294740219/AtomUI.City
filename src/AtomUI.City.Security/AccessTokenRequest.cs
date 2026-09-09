namespace AtomUI.City.Security;

public sealed class AccessTokenRequest
{
    public AccessTokenRequest(
        string resourceName,
        string? scheme = null,
        string? operationName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        ValidateOptional(scheme, nameof(scheme));
        ValidateOptional(operationName, nameof(operationName));

        ResourceName = resourceName;
        Scheme = scheme;
        OperationName = operationName;
    }

    public string ResourceName { get; }

    public string? Scheme { get; }

    public string? OperationName { get; }

    private static void ValidateOptional(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }
    }
}
