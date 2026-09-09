namespace AtomUI.City.Data;

internal static class DataErrorMessage
{
    public static string FromException(Exception exception, string fallback)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

        return string.IsNullOrWhiteSpace(exception.Message)
            ? fallback
            : exception.Message;
    }
}
