using System.Security.Cryptography;
using SsoExample.Api.Models;
using SsoExample.Api.Security;

namespace SsoExample.Api.Data;

public sealed class InMemorySsoStore
{
    private readonly List<UserRecord> _users =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "admin", "admin@example.test", PasswordHasher.Hash("Admin@123"), "SSO Admin", ["Admin"], true),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "alice", "alice@example.test", PasswordHasher.Hash("Alice@123"), "Alice Nguyen", ["User"], true),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "bob", "bob@example.test", PasswordHasher.Hash("Bob@123"), "Bob Tran", ["User", "Support"], true)
    ];

    private readonly List<ClientRecord> _clients =
    [
        new("ssoexample-web", "SSOExample.Web jQuery SPA", ["https://localhost:5002/auth/callback", "https://localhost:5002/"], true)
    ];

    private readonly List<AuthCodeRecord> _codes = [];
    private readonly List<RefreshTokenRecord> _refreshTokens = [];
    private readonly List<AuditLogRecord> _auditLogs = [];

    public IReadOnlyList<UserRecord> Users => _users;
    public IReadOnlyList<ClientRecord> Clients => _clients;
    public IReadOnlyList<AuditLogRecord> AuditLogs => _auditLogs.OrderByDescending(x => x.CreatedAt).ToList();

    public UserRecord? FindUser(string userNameOrEmail) =>
        _users.FirstOrDefault(x => string.Equals(x.UserName, userNameOrEmail, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(x.Email, userNameOrEmail, StringComparison.OrdinalIgnoreCase));

    public UserRecord? FindUser(Guid id) => _users.FirstOrDefault(x => x.Id == id);

    public ClientRecord? FindClient(string clientId) => _clients.FirstOrDefault(x => x.ClientId == clientId);

    public AuthCodeRecord CreateAuthCode(Guid userId, ClientRecord client, string redirectUri, string? codeChallenge)
    {
        var code = WebSafeRandom(32);
        var record = new AuthCodeRecord(code, userId, client.ClientId, redirectUri, codeChallenge, DateTimeOffset.UtcNow.AddMinutes(2));
        _codes.Add(record);
        return record;
    }

    public AuthCodeRecord? RedeemAuthCode(string code, string clientId, string redirectUri)
    {
        var record = _codes.FirstOrDefault(x => x.Code == code && x.ClientId == clientId && x.RedirectUri == redirectUri);
        if (record is null || record.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return null;
        }

        _codes.Remove(record);
        return record;
    }

    public RefreshTokenRecord CreateRefreshToken(Guid userId, string clientId, TimeSpan lifetime)
    {
        var record = new RefreshTokenRecord(WebSafeRandom(48), userId, clientId, DateTimeOffset.UtcNow.Add(lifetime), false);
        _refreshTokens.Add(record);
        return record;
    }

    public RefreshTokenRecord? RedeemRefreshToken(string token, string clientId)
    {
        var record = _refreshTokens.FirstOrDefault(x => x.Token == token && x.ClientId == clientId && !x.Revoked && x.ExpiresAt > DateTimeOffset.UtcNow);
        if (record is null)
        {
            return null;
        }

        _refreshTokens.Remove(record);
        return record;
    }

    public void AddAudit(Guid actorUserId, Guid? subjectUserId, string action, HttpContext httpContext, string? reason = null)
    {
        _auditLogs.Add(new AuditLogRecord(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            actorUserId,
            subjectUserId,
            action,
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            httpContext.Request.Headers.UserAgent.ToString(),
            reason));
    }

    private static string WebSafeRandom(int byteCount) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
