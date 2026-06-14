namespace AtomUI.City.Mvvm;

public sealed class ValidationChangedEventArgs : EventArgs
{
    public ValidationChangedEventArgs(
        string key,
        ValidationStatus status,
        IReadOnlyList<string> errors,
        IReadOnlyList<ValidationMessage> messages,
        Guid? ownerScopeId)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(messages);

        Key = key;
        Status = status;
        Errors = Array.AsReadOnly(errors.ToArray());
        Messages = Array.AsReadOnly(messages.ToArray());
        OwnerScopeId = ownerScopeId;
    }

    public string Key { get; }

    public ValidationStatus Status { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<ValidationMessage> Messages { get; }

    public Guid? OwnerScopeId { get; }
}
