using Microsoft.AspNetCore.Mvc;

namespace SsoExample.Api.Controllers;

[ApiController]
[Route("api")]
[Tags("Health")]
[Produces("application/json")]
public class HealthController(IConfiguration config) : ControllerBase
{
    /// <summary>Liveness probe và runtime info.</summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() => Ok(new
    {
        status = "ok",
        runtime = ".NET 10",
        service = "SsoExample.Api",
        timestamp = DateTimeOffset.UtcNow
    });

    /// <summary>Provider, authority, audience, scope hiện cấu hình của API.</summary>
    [HttpGet("info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Info() => Ok(new
    {
        service = "SsoExample.Api",
        provider = config["Authentication:Provider"],
        authority = config["Authentication:MicrosoftEntraId:Authority"],
        audience = config["Authentication:MicrosoftEntraId:Api:Audience"],
        scope = config["Authentication:MicrosoftEntraId:Api:Scopes:AccessAsUser"]
    });
}
