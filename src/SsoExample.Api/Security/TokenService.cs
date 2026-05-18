using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SsoExample.Api.Models;

namespace SsoExample.Api.Security;

public sealed class TokenService(IConfiguration configuration)
{
    private readonly string _issuer = configuration["Sso:Issuer"] ?? "https://localhost:5001";
    private readonly string _signingKey = configuration["Sso:SigningKey"] ?? "dev-only-change-this-64-byte-secret-before-production-use";
    private readonly int _accessTokenMinutes = int.TryParse(configuration["Sso:AccessTokenMinutes"], out var minutes) ? minutes : 15;
    private readonly int _loginAsMinutes = int.TryParse(configuration["Sso:LoginAsMinutes"], out var loginAsMinutes) ? loginAsMinutes : 10;

    public (string AccessToken, DateTimeOffset ExpiresAt) CreateAccessToken(UserProfile subject, string clientId, ImpersonationInfo? impersonation = null)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = impersonation?.ExpiresAt ?? now.AddMinutes(_accessTokenMinutes);
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = _issuer,
            ["aud"] = clientId,
            ["sub"] = subject.Id,
            ["name"] = subject.UserName,
            ["email"] = subject.Email,
            ["display_name"] = subject.DisplayName,
            ["roles"] = subject.Roles,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
            ["impersonation"] = impersonation
        };

        var header = new Dictionary<string, object?> { ["alg"] = "HS256", ["typ"] = "JWT" };
        var unsigned = $"{Base64Url(JsonSerializer.SerializeToUtf8Bytes(header))}.{Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload))}";
        return ($"{unsigned}.{Sign(unsigned)}", expiresAt);
    }

    public CurrentPrincipal? ValidateAccessToken(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var unsigned = $"{parts[0]}.{parts[1]}";
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sign(unsigned)), Encoding.UTF8.GetBytes(parts[2])))
        {
            return null;
        }

        using var json = JsonDocument.Parse(Base64UrlDecode(parts[1]));
        if (!json.RootElement.TryGetProperty("exp", out var exp) || DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()) < DateTimeOffset.UtcNow)
        {
            return null;
        }

        var roles = json.RootElement.GetProperty("roles").EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToArray();
        var impersonation = ReadImpersonation(json.RootElement);
        var email = json.RootElement.TryGetProperty("email", out var emailEl) ? emailEl.GetString() ?? string.Empty : string.Empty;
        var displayName = json.RootElement.TryGetProperty("display_name", out var dnEl) ? dnEl.GetString() ?? string.Empty : string.Empty;
        return new CurrentPrincipal(
            json.RootElement.GetProperty("sub").GetGuid(),
            json.RootElement.GetProperty("name").GetString() ?? string.Empty,
            email,
            displayName,
            roles,
            impersonation);
    }

    public ImpersonationInfo CreateImpersonation(UserProfile actor, UserProfile subject, string reason) =>
        new(actor.Id, actor.UserName, subject.Id, reason, DateTimeOffset.UtcNow.AddMinutes(_loginAsMinutes));

    private string Sign(string unsigned)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingKey));
        return Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsigned)));
    }

    private static ImpersonationInfo? ReadImpersonation(JsonElement root)
    {
        if (!root.TryGetProperty("impersonation", out var element) || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return element.Deserialize<ImpersonationInfo>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
