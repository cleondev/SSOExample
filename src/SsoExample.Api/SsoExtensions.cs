using System.Security.Claims;
using SsoExample.Api.Models;
using SsoExample.Api.Security;

namespace SsoExample.Api;

public static class SsoExtensions
{
    public static UserProfile ToProfile(this UserRecord user) =>
        new(user.Id, user.UserName, user.Email, user.DisplayName, user.Roles);

    public static CurrentPrincipal? RequireCurrentUser(this HttpContext http, TokenService tokens)
    {
        // 1. Microsoft Entra ID JWT validated bởi JwtBearer middleware.
        if (http.User.Identity?.IsAuthenticated == true && http.User.Identity.AuthenticationType is not null)
        {
            return BuildFromClaims(http.User);
        }

        // 2. Local JWT (HMAC-SHA256) cho demo password / login-as.
        var authorization = http.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return tokens.ValidateAccessToken(authorization["Bearer ".Length..].Trim());
    }

    private static CurrentPrincipal BuildFromClaims(ClaimsPrincipal principal)
    {
        // oid = stable Azure object ID. Fallback sang sub/nameidentifier nếu thiếu.
        var oid = principal.FindFirstValue("oid")
                  ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
                  ?? principal.FindFirstValue("sub")
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? Guid.Empty.ToString();
        Guid.TryParse(oid, out var userId);

        // v2.0: preferred_username. v1.0: upn / unique_name. Fallback cuối: name.
        var userName = principal.FindFirstValue("preferred_username")
                       ?? principal.FindFirstValue("upn")
                       ?? principal.FindFirstValue("unique_name")
                       ?? principal.FindFirstValue("email")
                       ?? principal.FindFirstValue("name")
                       ?? principal.Identity?.Name
                       ?? "user";

        var email = principal.FindFirstValue("email")
                    ?? principal.FindFirstValue("upn")
                    ?? principal.FindFirstValue("unique_name")
                    ?? principal.FindFirstValue("preferred_username")
                    ?? string.Empty;

        var displayName = principal.FindFirstValue("name")
                          ?? userName;

        var roles = principal.FindAll("roles")
            .Concat(principal.FindAll("http://schemas.microsoft.com/ws/2008/06/identity/claims/role"))
            .Concat(principal.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new CurrentPrincipal(userId, userName, email, displayName, roles, null);
    }
}
