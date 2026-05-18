using Microsoft.AspNetCore.Mvc;
using SsoExample.Api.Data;
using SsoExample.Api.Models;
using SsoExample.Api.Security;

namespace SsoExample.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Tags("Auth")]
[Produces("application/json")]
public class AuthController(InMemorySsoStore store, TokenService tokens, IConfiguration config) : ControllerBase
{
    /// <summary>Login bằng username/password (chỉ dùng cho local demo).</summary>
    [HttpPost("login/password")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<TokenResponse> LoginPassword([FromBody] LoginPasswordRequest request)
    {
        var client = store.FindClient(request.ClientId);
        if (client is null)
        {
            return BadRequest(new { error = "invalid_client" });
        }

        var user = store.FindUser(request.UserNameOrEmail);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized();
        }

        store.AddAudit(user.Id, user.Id, "password_login", HttpContext);
        return Ok(TokenResponseFactory.Create(user.ToProfile(), request.ClientId, store, tokens, config));
    }

    /// <summary>Refresh access token. Token cũ bị xoá sau khi đổi (rotation).</summary>
    [HttpPost("token/refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<TokenResponse> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var refreshToken = store.RedeemRefreshToken(request.RefreshToken, request.ClientId);
        if (refreshToken is null)
        {
            return Unauthorized();
        }

        var user = store.FindUser(refreshToken.UserId);
        return user is null
            ? Unauthorized()
            : Ok(TokenResponseFactory.Create(user.ToProfile(), request.ClientId, store, tokens, config));
    }

    /// <summary>Admin impersonate một user khác. Bắt buộc role Admin + reason ≥ 10 ký tự.</summary>
    [HttpPost("login-as")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<TokenResponse> LoginAs([FromBody] LoginAsRequest request)
    {
        var actor = HttpContext.RequireCurrentUser(tokens);
        if (actor is null || !actor.Roles.Contains("Admin"))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 10)
        {
            return BadRequest(new { error = "reason_min_10_characters" });
        }

        var subjectUser = store.FindUser(request.TargetUserId);
        if (subjectUser is null || !subjectUser.IsActive)
        {
            return NotFound();
        }

        // Actor có thể là Microsoft Entra user (không nằm trong InMemoryStore) hoặc local seeded user.
        // Build actor profile trực tiếp từ claims principal đã validate.
        var actorProfile = new UserProfile(
            actor.UserId,
            actor.UserName,
            string.IsNullOrEmpty(actor.Email) ? actor.UserName : actor.Email,
            string.IsNullOrEmpty(actor.DisplayName) ? actor.UserName : actor.DisplayName,
            actor.Roles);

        var impersonation = tokens.CreateImpersonation(actorProfile, subjectUser.ToProfile(), request.Reason);
        store.AddAudit(actor.UserId, subjectUser.Id, "login_as", HttpContext, request.Reason);
        return Ok(TokenResponseFactory.Create(subjectUser.ToProfile(), request.ClientId, store, tokens, config, impersonation));
    }
}
