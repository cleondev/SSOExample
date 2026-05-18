using Microsoft.AspNetCore.Mvc;
using SsoExample.Api.Models;
using SsoExample.Api.Security;

namespace SsoExample.Api.Controllers;

[ApiController]
[Route("api")]
[Tags("Business")]
[Produces("application/json")]
public class BusinessController(TokenService tokens) : ControllerBase
{
    /// <summary>Trả về principal hiện tại theo bearer token.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentPrincipal), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentPrincipal> Me()
    {
        var current = HttpContext.RequireCurrentUser(tokens);
        return current is null ? Unauthorized() : Ok(current);
    }

    /// <summary>Endpoint nghiệp vụ ví dụ; cần bearer token hợp lệ.</summary>
    [HttpGet("orders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Orders()
    {
        var current = HttpContext.RequireCurrentUser(tokens);
        return current is null
            ? Unauthorized()
            : Ok(new[]
            {
                new { id = 1001, owner = current.UserName, total = 199.95, status = "paid" },
                new { id = 1002, owner = current.UserName, total = 49.50, status = "processing" }
            });
    }
}
