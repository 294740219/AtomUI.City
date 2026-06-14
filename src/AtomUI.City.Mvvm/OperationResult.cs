namespace AtomUI.City.Mvvm;

public sealed record OperationResult(
    Guid OperationId,
    OperationStatus Status,
    TimeSpan Elapsed,
    Exception? Error = null);
