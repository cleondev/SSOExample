using SsoExample.Api;
using SsoExample.Api.Data;
using SsoExample.Api.Security;

namespace SsoExample.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin");

        group.MapGet("/users", (HttpContext http, TokenService tokens, InMemorySsoStore store) =>
        {
            var current = http.RequireCurrentUser(tokens);
            return current is null || !current.Roles.Contains("Admin")
                ? Results.Forbid()
                : Results.Ok(store.Users.Select(x => x.ToProfile()));
        });

        group.MapGet("/audit-logs", (HttpContext http, TokenService tokens, InMemorySsoStore store) =>
        {
            var current = http.RequireCurrentUser(tokens);
            return current is null || !current.Roles.Contains("Admin")
                ? Results.Forbid()
                : Results.Ok(store.AuditLogs);
        });

        return app;
    }
}
