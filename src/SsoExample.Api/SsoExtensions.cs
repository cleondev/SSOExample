using SsoExample.Api.Models;
using SsoExample.Api.Security;

namespace SsoExample.Api;

public static class SsoExtensions
{
    public static UserProfile ToProfile(this UserRecord user) =>
        new(user.Id, user.UserName, user.Email, user.DisplayName, user.Roles);

    public static CurrentPrincipal? RequireCurrentUser(this HttpContext http, TokenService tokens)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return tokens.ValidateAccessToken(authorization["Bearer ".Length..].Trim());
    }
}
