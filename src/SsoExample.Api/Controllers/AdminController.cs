using Microsoft.AspNetCore.Mvc;
using SsoExample.Api.Data;
using SsoExample.Api.Models;
using SsoExample.Api.Security;

namespace SsoExample.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Tags("Admin")]
[Produces("application/json")]
public class AdminController(InMemorySsoStore store, TokenService tokens) : ControllerBase
{
    /// <summary>Danh sách user (chỉ Admin).</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IEnumerable<UserProfile>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<IEnumerable<UserProfile>> Users()
    {
        var current = HttpContext.RequireCurrentUser(tokens);
        return current is null || !current.Roles.Contains("Admin")
            ? Forbid()
            : Ok(store.Users.Select(x => x.ToProfile()));
    }

    /// <summary>Audit log: password_login, sso_authorize, login_as (chỉ Admin).</summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(IEnumerable<AuditLogRecord>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<IEnumerable<AuditLogRecord>> AuditLogs()
    {
        var current = HttpContext.RequireCurrentUser(tokens);
        return current is null || !current.Roles.Contains("Admin")
            ? Forbid()
            : Ok(store.AuditLogs);
    }

    /// <summary>Request history: ghi lại ai gọi endpoint nào, status code, IP (chỉ Admin).</summary>
    [HttpGet("request-logs")]
    [ProducesResponseType(typeof(IEnumerable<RequestLogRecord>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<IEnumerable<RequestLogRecord>> RequestLogs()
    {
        var current = HttpContext.RequireCurrentUser(tokens);
        return current is null || !current.Roles.Contains("Admin")
            ? Forbid()
            : Ok(store.RequestLogs);
    }
}
