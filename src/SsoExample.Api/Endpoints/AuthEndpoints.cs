using SsoExample.Api;
using SsoExample.Api.Data;
using SsoExample.Api.Models;
using SsoExample.Api.Security;

namespace SsoExample.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login/password", (LoginPasswordRequest request, InMemorySsoStore store, TokenService tokens, IConfiguration config, HttpContext http) =>
        {
            var client = store.FindClient(request.ClientId);
            if (client is null)
            {
                return Results.BadRequest(new { error = "invalid_client" });
            }

            var user = store.FindUser(request.UserNameOrEmail);
            if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            store.AddAudit(user.Id, user.Id, "password_login", http);
            return Results.Ok(TokenResponseFactory.Create(user.ToProfile(), request.ClientId, store, tokens, config));
        });

        group.MapPost("/token/refresh", (RefreshTokenRequest request, InMemorySsoStore store, TokenService tokens, IConfiguration config) =>
        {
            var refreshToken = store.RedeemRefreshToken(request.RefreshToken, request.ClientId);
            if (refreshToken is null)
            {
                return Results.Unauthorized();
            }

            var user = store.FindUser(refreshToken.UserId);
            return user is null
                ? Results.Unauthorized()
                : Results.Ok(TokenResponseFactory.Create(user.ToProfile(), request.ClientId, store, tokens, config));
        });

        group.MapPost("/login-as", (LoginAsRequest request, InMemorySsoStore store, TokenService tokens, IConfiguration config, HttpContext http) =>
        {
            var actor = http.RequireCurrentUser(tokens);
            if (actor is null || !actor.Roles.Contains("Admin"))
            {
                return Results.Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 10)
            {
                return Results.BadRequest(new { error = "reason_min_10_characters" });
            }

            var actorUser = store.FindUser(actor.UserId);
            var subjectUser = store.FindUser(request.TargetUserId);
            if (actorUser is null || subjectUser is null || !subjectUser.IsActive)
            {
                return Results.NotFound();
            }

            var impersonation = tokens.CreateImpersonation(actorUser.ToProfile(), subjectUser.ToProfile(), request.Reason);
            store.AddAudit(actorUser.Id, subjectUser.Id, "login_as", http, request.Reason);
            return Results.Ok(TokenResponseFactory.Create(subjectUser.ToProfile(), request.ClientId, store, tokens, config, impersonation));
        });

        return app;
    }
}
