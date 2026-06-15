namespace AtomUI.City.Security;

public enum AccessTokenResultStatus
{
    None,
    Success,
    Required,
    Expired,
    Failed,
    Unavailable,
    Cancelled,
}
