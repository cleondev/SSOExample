using SsoExample.Api.Data;
using SsoExample.Api.Models;

namespace SsoExample.Api.Security;

public static class TokenResponseFactory
{
    public static TokenResponse Create(
        UserProfile user,
        string clientId,
        InMemorySsoStore store,
        TokenService tokens,
        IConfiguration config,
        ImpersonationInfo? impersonation = null)
    {
        var accessToken = tokens.CreateAccessToken(user, clientId, impersonation);
        var refreshDays = int.TryParse(config["Sso:RefreshTokenDays"], out var days) ? days : 14;
        var refreshToken = store.CreateRefreshToken(user.Id, clientId, TimeSpan.FromDays(refreshDays));
        return new TokenResponse(accessToken.AccessToken, refreshToken.Token, accessToken.ExpiresAt, user, impersonation);
    }
}
