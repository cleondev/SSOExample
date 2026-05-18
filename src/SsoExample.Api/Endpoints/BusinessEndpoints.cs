using SsoExample.Api;
using SsoExample.Api.Security;

namespace SsoExample.Api.Endpoints;

public static class BusinessEndpoints
{
    public static IEndpointRouteBuilder MapBusinessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Business");

        group.MapGet("/me", (HttpContext http, TokenService tokens) =>
        {
            var current = http.RequireCurrentUser(tokens);
            return current is null ? Results.Unauthorized() : Results.Ok(current);
        });

        group.MapGet("/orders", (HttpContext http, TokenService tokens) =>
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

        return app;
    }
}
