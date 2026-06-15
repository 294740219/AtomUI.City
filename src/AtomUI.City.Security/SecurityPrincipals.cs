using System.Security.Claims;

namespace AtomUI.City.Security;

public static class SecurityPrincipals
{
    public static ClaimsPrincipal Anonymous => new(new ClaimsIdentity());
}
