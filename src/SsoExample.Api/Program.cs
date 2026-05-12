using SsoExample.Api;
using SsoExample.Api.Data;
using SsoExample.Api.Models;
using SsoExample.Api.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<InMemorySsoStore>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true)));

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", runtime = ".NET 10", service = "SsoExample.Api" }));

app.MapPost("/api/auth/login/password", (LoginPasswordRequest request, InMemorySsoStore store, TokenService tokens, IConfiguration config, HttpContext http) =>
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
    return Results.Ok(CreateTokenResponse(user.ToProfile(), request.ClientId, store, tokens, config));
});

app.MapGet("/api/sso/authorize", (string client_id, string redirect_uri, string state, string? code_challenge, InMemorySsoStore store, HttpContext http) =>
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

app.MapPost("/api/sso/token", (ExchangeCodeRequest request, InMemorySsoStore store, TokenService tokens, IConfiguration config) =>
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
    return user is null ? Results.Unauthorized() : Results.Ok(CreateTokenResponse(user.ToProfile(), request.ClientId, store, tokens, config));
});

app.MapPost("/api/auth/token/refresh", (RefreshTokenRequest request, InMemorySsoStore store, TokenService tokens, IConfiguration config) =>
{
    var refreshToken = store.RedeemRefreshToken(request.RefreshToken, request.ClientId);
    if (refreshToken is null)
    {
        return Results.Unauthorized();
    }

    var user = store.FindUser(refreshToken.UserId);
    return user is null ? Results.Unauthorized() : Results.Ok(CreateTokenResponse(user.ToProfile(), request.ClientId, store, tokens, config));
});

app.MapPost("/api/auth/login-as", (LoginAsRequest request, InMemorySsoStore store, TokenService tokens, IConfiguration config, HttpContext http) =>
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
    return Results.Ok(CreateTokenResponse(subjectUser.ToProfile(), request.ClientId, store, tokens, config, impersonation));
});

app.MapGet("/api/me", (HttpContext http, TokenService tokens) =>
{
    var current = http.RequireCurrentUser(tokens);
    return current is null ? Results.Unauthorized() : Results.Ok(current);
});

app.MapGet("/api/orders", (HttpContext http, TokenService tokens) =>
{
    var current = http.RequireCurrentUser(tokens);
    return current is null
        ? Results.Unauthorized()
        : Results.Ok(new[]
        {
            new { id = 1001, owner = current.UserName, total = 199.95, status = "paid" },
            new { id = 1002, owner = current.UserName, total = 49.50, status = "processing" }
        });
});

app.MapGet("/api/admin/users", (HttpContext http, TokenService tokens, InMemorySsoStore store) =>
{
    var current = http.RequireCurrentUser(tokens);
    return current is null || !current.Roles.Contains("Admin")
        ? Results.Forbid()
        : Results.Ok(store.Users.Select(x => x.ToProfile()));
});

app.MapGet("/api/admin/audit-logs", (HttpContext http, TokenService tokens, InMemorySsoStore store) =>
{
    var current = http.RequireCurrentUser(tokens);
    return current is null || !current.Roles.Contains("Admin")
        ? Results.Forbid()
        : Results.Ok(store.AuditLogs);
});

app.MapFallbackToFile("index.html");
app.Run();

static TokenResponse CreateTokenResponse(UserProfile user, string clientId, InMemorySsoStore store, TokenService tokens, IConfiguration config, ImpersonationInfo? impersonation = null)
{
    var accessToken = tokens.CreateAccessToken(user, clientId, impersonation);
    var refreshDays = int.TryParse(config["Sso:RefreshTokenDays"], out var days) ? days : 14;
    var refreshToken = store.CreateRefreshToken(user.Id, clientId, TimeSpan.FromDays(refreshDays));
    return new TokenResponse(accessToken.AccessToken, refreshToken.Token, accessToken.ExpiresAt, user, impersonation);
}
