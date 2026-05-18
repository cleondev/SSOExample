using SsoExample.Api.Data;
using SsoExample.Api.Models;

namespace SsoExample.Api.Security;

public sealed class RequestLogMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SkipPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/health",
        "/api/info",
        "/api/admin/request-logs"
    };

    public async Task InvokeAsync(HttpContext context, InMemorySsoStore store, TokenService tokens)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var shouldLog = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                        && !SkipPaths.Contains(path);

        var principal = shouldLog ? context.RequireCurrentUser(tokens) : null;
        var method = context.Request.Method;
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers.UserAgent.ToString();

        await next(context);

        if (!shouldLog)
        {
            return;
        }

        store.AddRequestLog(new RequestLogRecord(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            principal?.UserId,
            principal?.UserName,
            method,
            path,
            context.Response.StatusCode,
            ip,
            userAgent,
            principal?.Impersonation is not null,
            principal?.Impersonation?.ActorUserId,
            principal?.Impersonation?.ActorUserName));
    }
}
