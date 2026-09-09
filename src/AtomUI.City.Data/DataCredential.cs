namespace AtomUI.City.Data;

public sealed record DataCredential
{
    public DataCredential(string scheme, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);

        Scheme = scheme;
        Parameter = parameter;
    }

    public string Scheme { get; }

    public string Parameter { get; }

    public static DataCredential Bearer(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return new DataCredential("Bearer", token);
    }

    public override string ToString() =>
        $"{nameof(DataCredential)} {{ Scheme = {Scheme}, Parameter = [REDACTED] }}";
}
