namespace SsoExample.Api.Models;

public sealed record UserRecord(
    Guid Id,
    string UserName,
    string Email,
    string PasswordHash,
    string DisplayName,
    string[] Roles,
    bool IsActive);

public sealed record ClientRecord(
    string ClientId,
    string Name,
    string[] RedirectUris,
    bool RequirePkce);

public sealed record AuthCodeRecord(
    string Code,
    Guid UserId,
    string ClientId,
    string RedirectUri,
    string? CodeChallenge,
    DateTimeOffset ExpiresAt);

public sealed record RefreshTokenRecord(
    string Token,
    Guid UserId,
    string ClientId,
    DateTimeOffset ExpiresAt,
    bool Revoked);

public sealed record AuditLogRecord(
    Guid Id,
    DateTimeOffset CreatedAt,
    Guid ActorUserId,
    Guid? SubjectUserId,
    string Action,
    string IpAddress,
    string UserAgent,
    string? Reason);

public sealed record LoginPasswordRequest(string UserNameOrEmail, string Password, string ClientId = "jquery-spa");
public sealed record LoginAsRequest(Guid TargetUserId, string Reason, string ClientId = "jquery-spa");
public sealed record RefreshTokenRequest(string RefreshToken, string ClientId = "jquery-spa");
public sealed record ExchangeCodeRequest(string Code, string RedirectUri, string ClientId, string? CodeVerifier);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, UserProfile User, ImpersonationInfo? Impersonation);
public sealed record UserProfile(Guid Id, string UserName, string Email, string DisplayName, string[] Roles);
public sealed record ImpersonationInfo(Guid ActorUserId, string ActorUserName, Guid SubjectUserId, string Reason, DateTimeOffset ExpiresAt);
public sealed record CurrentPrincipal(Guid UserId, string UserName, string[] Roles, ImpersonationInfo? Impersonation);
