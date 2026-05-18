using SsoExample.Api;
using SsoExample.Api.Data;
using SsoExample.Api.Models;
using SsoExample.Api.Security;

namespace SsoExample.Api.Endpoints;

public static class SsoEndpoints
{
    public static IEndpointRouteBuilder MapSsoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sso").WithTags("SSO");

        group.MapGet("/authorize", (string client_id, string redirect_uri, string state, string? code_challenge, InMemorySsoStore store, HttpContext http) =>
        {
            var client = store.FindClient(client_id);
            if (client is null || !client.RedirectUris.Contains(redirect_uri, StringComparer.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "invalid_client_or_redirect_uri" });
            }

            var demoUser = store.FindUser("alice")!;
            var code = store.CreateAuthCode(demoUser.Id, client, redirect_uri, code_challenge);
            store.AddAudit(demoUser.Id, demoUser.Id, "sso_authorize_demo_session", http);
            return Results.Redirect($"{redirect_uri}?code={Uri.EscapeDataString(code.Code)}&state={Uri.EscapeDataString(state)}");
        });

        group.MapPost("/token", (ExchangeCodeRequest request, InMemorySsoStore store, TokenService tokens, IConfiguration config) =>
        {
            var client = store.FindClient(request.ClientId);
            if (client is null || !client.RedirectUris.Contains(request.RedirectUri, StringComparer.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "invalid_client_or_redirect_uri" });
            }

            var code = store.RedeemAuthCode(request.Code, request.ClientId, request.RedirectUri);
            if (code is null || !TokenService.ValidatePkce(request.CodeVerifier, code.CodeChallenge))
            {
                return Results.BadRequest(new { error = "invalid_grant" });
            }

            var user = store.FindUser(code.UserId);
            return user is null
                ? Results.Unauthorized()
                : Results.Ok(TokenResponseFactory.Create(user.ToProfile(), request.ClientId, store, tokens, config));
        });

        return app;
    }
}
