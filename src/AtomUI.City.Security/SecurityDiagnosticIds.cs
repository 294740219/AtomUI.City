namespace AtomUI.City.Security;

public static class SecurityDiagnosticIds
{
    public const string AuthenticationStateChanged = "AUCSEC001";

    public const string AuthenticationObserverFailed = "AUCSEC002";

    public const string PermissionRegistryChanged = "AUCSEC101";

    public const string PermissionObserverFailed = "AUCSEC102";

    public const string AuthorizationDenied = "AUCSEC200";

    public const string AuthorizationEvaluationFailed = "AUCSEC201";

    public const string RouteAuthorizationCompleted = "AUCSEC300";

    public const string RouteAuthorizationFailed = "AUCSEC301";

    public const string CommandAuthorizationChanged = "AUCSEC400";

    public const string CommandAuthorizationFailed = "AUCSEC401";

    public const string CommandAuthorizationObserverFailed = "AUCSEC402";

    public const string CommandAuthorizationEvaluated = "AUCSEC403";

    public const string AccessTokenResolved = "AUCSEC500";

    public const string AccessTokenProviderFailed = "AUCSEC501";
}
