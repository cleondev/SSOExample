# Hướng dẫn fresher: triển khai SSO cho một site khác giống SSOExample

Tài liệu này hướng đến **fresher .NET / web** lần đầu làm SSO. Mục tiêu: cầm tay chỉ
việc từ Azure Portal cho tới appsettings, đến code Web/API, để bạn có thể nhân
bản mô hình của repo này (Web jQuery SPA + API .NET 10) sang một site mới.

Đọc kèm:

- `docs/sso-design.md` — kiến trúc và flow tổng quan.
- `docs/database-schema.sql` — DDL bảng cần có cho production.
- README.md — quickstart và endpoint.

---

## 0. Bức tranh tổng

Bạn sẽ build hai thứ:

| Vai trò | Project tham chiếu | Trên Azure |
| --- | --- | --- |
| **Web** (public client, chạy code trên browser) | `src/SsoExample.Web` | App registration `<TênSite>.Web`, platform **Single-page application** |
| **API** (resource server, validate token) | `src/SsoExample.Api` | App registration `<TênSite>.Api`, **Expose an API** |

Flow chuẩn cần nhớ:

```
Browser → Web SPA → (PKCE) → Microsoft Entra ID → Web SPA → (Bearer) → API → Business DB
```

Web không bao giờ giữ `client_secret`, không bao giờ thấy mật khẩu user. API không
bao giờ tự cấp token; chỉ validate.

---

## 1. Chuẩn bị

- .NET 10 SDK.
- Quyền tạo App registration trên Microsoft Entra ID (Azure AD) của tenant.
- HTTPS dev cert: `dotnet dev-certs https --trust`.
- Trình duyệt hỗ trợ `crypto.subtle` (mọi browser hiện đại).

---

## 2. Tạo app registration trên Azure

> Mục tiêu: lấy 4 giá trị — Tenant ID, API client ID, API App ID URI, Web client ID — để điền vào appsettings.

### 2.1. App registration cho **API** (`<TênSite>.Api`)

1. Azure Portal → **Microsoft Entra ID** → **App registrations** → **New registration**.
2. Name = `<TênSite>.Api`, supported account types theo nhu cầu (thường là "single tenant").
3. **Không** điền redirect URI cho API.
4. Sau khi tạo, copy `Directory (tenant) ID` và `Application (client) ID`.
5. Vào **Expose an API**:
   - **Application ID URI** = `api://<API client ID>` (Azure gợi ý sẵn).
   - **Add a scope**: tên scope `access_as_user`, admin + user consent, display name "Access <TênSite> API as signed-in user".
6. (Tuỳ chọn) **App roles** nếu muốn phân quyền chuẩn theo Entra ID: tạo `Admin`, `Support`, `User` với `value` y hệt và allowed member types = **Users/Groups**.

### 2.2. App registration cho **Web** (`<TênSite>.Web`)

1. **New registration** lần nữa, name = `<TênSite>.Web`.
2. **Redirect URI**: platform **Single-page application (SPA)** → `https://<host-web>/auth/callback`. Dev thường là `https://localhost:5002/auth/callback`.
3. Copy `Application (client) ID` của Web.
4. **API permissions** → **Add a permission** → **My APIs** → chọn `<TênSite>.Api` → tick `access_as_user` → **Add permissions**. Nếu tenant yêu cầu, bấm **Grant admin consent**.
5. **Authentication** → bật **Allow public client flows** = `No` (SPA không cần), không tạo client secret.
6. (Tuỳ chọn) **Front-channel logout URL** = `https://<host-web>/` nếu muốn logout đồng bộ.

### 2.3. Checklist sau khi tạo

- [ ] API expose scope `access_as_user`.
- [ ] Web có delegated permission tới scope đó và đã grant consent.
- [ ] Redirect URI Web khớp **chính xác** giá trị bạn sẽ dùng trong code (https, port, đường dẫn).
- [ ] Web là **Single-page application**, không phải Web hay Mobile.
- [ ] Không có client secret nào ở Web.

---

## 3. Điền giá trị vào appsettings

Copy file thay thế bằng giá trị thật. Để giữ secret out-of-repo: dùng [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) khi dev, **Azure Key Vault / App Configuration** khi prod.

### 3.1. API — `src/<TênSite>.Api/appsettings.Required.json`

```json
{
  "Authentication": {
    "Provider": "MicrosoftEntraId",
    "MicrosoftEntraId": {
      "TenantId": "<tenant-id>",
      "Authority": "https://login.microsoftonline.com/<tenant-id>/v2.0",
      "Api": {
        "ClientId": "<api-client-id>",
        "ApplicationIdUri": "api://<api-client-id>",
        "Audience": "api://<api-client-id>",
        "Scopes": { "AccessAsUser": "access_as_user" }
      },
      "AllowedClientApplications": [
        { "ClientId": "<web-client-id>" }
      ]
    }
  }
}
```

### 3.2. Web — `src/<TênSite>.Web/appsettings.Required.json`

```json
{
  "MicrosoftEntraId": {
    "TenantId": "<tenant-id>",
    "Authority": "https://login.microsoftonline.com/<tenant-id>/v2.0",
    "ClientId": "<web-client-id>"
  },
  "Api": {
    "BaseUrl": "https://<api-host>",
    "Audience": "api://<api-client-id>",
    "RequiredScope": "api://<api-client-id>/access_as_user"
  }
}
```

`appsettings.Optional.json` giữ redirect URI, scopes danh sách, post-logout, cache location và tên hiển thị tenant. Xem file mẫu trong repo để bám pattern.

### 3.3. Mapping nhanh

| Azure value | Điền vào |
| --- | --- |
| Directory (tenant) ID | `MicrosoftEntraId:TenantId` của cả API & Web |
| API client ID | `Authentication:MicrosoftEntraId:Api:ClientId` (API) và dùng trong scope ở Web |
| API App ID URI | `Authentication:MicrosoftEntraId:Api:ApplicationIdUri` & `Audience` (API), `Api:Audience` (Web) |
| Web client ID | `Authentication:MicrosoftEntraId:AllowedClientApplications[].ClientId` (API), `MicrosoftEntraId:ClientId` (Web) |
| Web redirect URI | `MicrosoftEntraId:RedirectUris` (Web Optional) — và phải khớp Azure |

---

## 4. Port code từ SSOExample sang site mới

### 4.1. Cấu trúc thư mục đề xuất

```
src/
  <TênSite>.Api/
    Program.cs
    Endpoints/        # AuthEndpoints, SsoEndpoints, AdminEndpoints, BusinessEndpoints
    Security/         # TokenService, PasswordHasher (chỉ dùng cho local demo)
    Data/             # Store / DbContext
    Models/
    wwwroot/          # landing page cho dev portal
  <TênSite>.Web/
    Program.cs        # host static SPA, expose /config
    wwwroot/
      index.html
      app.js
      site.css
```

Pattern endpoint module:

```csharp
public static class XEndpoints
{
    public static IEndpointRouteBuilder MapXEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/x").WithTags("X");
        group.MapGet("/", () => Results.Ok(...));
        return app;
    }
}
```

`Program.cs` chỉ wire DI + middleware + gọi `app.MapXEndpoints()` cho từng nhóm. Đừng nhồi mọi endpoint vào một file.

### 4.2. Validate token bằng `Microsoft.Identity.Web` (production)

Demo hiện validate JWT bằng `TokenService` tự viết. **Khi chuyển sang Entra ID thật**, đổi sang `Microsoft.Identity.Web`:

```bash
dotnet add src/<TênSite>.Api package Microsoft.Identity.Web
```

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("Authentication:MicrosoftEntraId"));

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new {
    sub = user.FindFirstValue("sub"),
    name = user.Identity?.Name,
    roles = user.FindAll("roles").Select(c => c.Value)
})).RequireAuthorization();
```

Trong appsettings, section `Authentication:MicrosoftEntraId` cần `Instance`, `TenantId`, `ClientId`, `Audience`, `Scopes` (delimited). Tham khảo docs Microsoft.

### 4.3. Web SPA: PKCE thực tế bằng MSAL.js

Demo gọi `/api/sso/authorize` của API local. Production thay bằng **MSAL Browser**:

```html
<script src="https://alcdn.msauth.net/browser/3.x/js/msal-browser.min.js"></script>
```

```js
const msal = new msal.PublicClientApplication({
  auth: {
    clientId: config.clientId,
    authority: config.authority,
    redirectUri: config.redirectUri
  },
  cache: { cacheLocation: 'sessionStorage' }
});

await msal.initialize();
await msal.handleRedirectPromise();

async function signIn() {
  await msal.loginRedirect({ scopes: [config.scope] });
}

async function getAccessToken() {
  const account = msal.getAllAccounts()[0];
  const result = await msal.acquireTokenSilent({ account, scopes: [config.scope] })
    .catch(() => msal.acquireTokenRedirect({ scopes: [config.scope] }));
  return result.accessToken;
}
```

`config.scope` = `api://<api-client-id>/access_as_user`. MSAL lo giúp PKCE, state, nonce, token cache, refresh.

### 4.4. Pattern gọi API

```js
async function api(path, options = {}) {
  const token = await getAccessToken();
  const res = await fetch(`${config.apiBaseUrl}${path}`, {
    ...options,
    headers: { ...(options.headers||{}), Authorization: `Bearer ${token}` }
  });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}
```

### 4.5. CORS

API cần cho phép origin của Web. Demo mở `AllowAnyOrigin` qua `SetIsOriginAllowed(_ => true)` cho dev nhanh. Prod **bắt buộc** allowlist:

```csharp
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("https://<host-web>").AllowAnyHeader().WithMethods("GET", "POST")));
```

---

## 5. Login-as (impersonation) — copy đúng nguyên tắc

Tính năng nhạy cảm. Khi port:

- Chỉ role `Admin` / `SupportSupervisor` được gọi.
- Bắt buộc `reason` ≥ 10 ký tự.
- Token impersonation sống ngắn (5–10 phút), **không phát refresh token** dài hạn.
- UI hiển thị **banner đỏ** suốt phiên (xem `index.html` template của repo).
- Audit log bất biến: actor, subject, lý do, IP, user-agent, thời điểm.
- Không cho impersonate admin khác nếu không có approval.

Code mẫu nằm ở `src/SsoExample.Api/Endpoints/AuthEndpoints.cs::MapPost("/login-as", ...)`.

---

## 6. Database

`docs/database-schema.sql` là DDL tham chiếu (PostgreSQL). Bảng cần có:

- `users`, `user_passwords`, `roles`, `user_roles`
- `clients`, `client_redirect_uris`
- `auth_codes`, `refresh_tokens`
- `impersonation_sessions`, `audit_logs`

Khi production-ize:

- Hash password bằng Argon2id hoặc bcrypt (demo dùng PBKDF2 với salt cố định — **không** dùng cho prod).
- Refresh token: lưu **hash** trong DB, không lưu plaintext. Hỗ trợ rotation + revoke.
- Auth code: cũng chỉ lưu hash. TTL ngắn (≤ 2 phút). Dùng 1 lần.
- Audit log: append-only. Idea: dùng schema riêng, không cho service user DELETE.

---

## 7. Checklist trước khi merge SSO vào site mới

- [ ] HTTPS bắt buộc cho cả Web và API trong mọi environment trên dev local.
- [ ] CORS allowlist đúng origin, không `AllowAnyOrigin` ở production.
- [ ] Token validation đầy đủ: `iss`, `aud`, `exp`, `nbf`, signature, scope, role.
- [ ] Web không log access token / refresh token ra console hay analytics.
- [ ] `client_secret` không nằm trong appsettings/JS/file tĩnh.
- [ ] Refresh token rotation + revoke trên logout.
- [ ] Rate limit + lockout cho login local nếu vẫn giữ flow đó.
- [ ] MFA cho admin và mọi hành động login-as.
- [ ] Audit log cover: login, logout, refresh, login-as, revoke, admin action.
- [ ] Observability: structured log + trace ID đính trên mọi request giữa Web ↔ API.
- [ ] Test bằng tài khoản không phải admin để xác nhận role gating thực sự chặn.

---

## 8. Lỗi thường gặp khi mới làm SSO

| Triệu chứng | Nguyên nhân hay gặp |
| --- | --- |
| `AADSTS50011: redirect URI mismatch` | Redirect URI trong code khác Azure (sai port, http vs https, có/không trailing slash). |
| `AADSTS65001: consent` | Quên grant admin consent cho scope `access_as_user`. |
| `invalid_grant` khi đổi code | `code_verifier` không khớp `code_challenge`, hoặc đã đổi code rồi. |
| API trả `401` ngay với token hợp lệ | `Audience` cấu hình ở API không khớp token (thiếu `api://` hoặc khác client ID). |
| Token có scope nhưng `roles` rỗng | Chưa assign user/group vào App role trong **Enterprise applications**. |
| CORS preflight `OPTIONS` 401 | Đặt `UseCors` sau `UseAuthentication` hoặc thiếu method `OPTIONS` trong allowlist. |
| `state` mismatch ở callback | Mở nhiều tab đăng nhập song song hoặc xoá storage giữa chừng. |

---

## 9. Gợi ý mở rộng

- Thay `InMemorySsoStore` bằng EF Core + migration; dùng `docs/database-schema.sql` làm tham chiếu.
- Thay JWT tự viết bằng **OpenIddict** hoặc **Duende IdentityServer** nếu cần tự host IdP.
- Thêm **Health checks** (`AddHealthChecks().AddCheck(...)`) và `/health/live`, `/health/ready`.
- Thêm **OpenAPI/Swagger UI** cho API: `dotnet add package Microsoft.AspNetCore.OpenApi` rồi `app.MapOpenApi()`.
- Trên Web, tách `app.js` thành module ESM (`auth.js`, `api.js`, `router.js`) khi codebase lớn hơn ~300 dòng.

---

## 10. Cheat sheet 1 phút

```
1. Tạo 2 app registration: <Site>.Api + <Site>.Web
2. Expose scope api://<api>/access_as_user
3. Web (SPA) xin permission access_as_user, grant consent
4. Điền TenantId / ClientId / Audience / RedirectUri vào appsettings
5. Web dùng MSAL Browser: loginRedirect → acquireTokenSilent
6. API dùng Microsoft.Identity.Web: AddMicrosoftIdentityWebApi + RequireAuthorization
7. Validate audience + scope + role trên mọi endpoint
8. Audit log mọi action nhạy cảm
9. CORS allowlist + HTTPS bắt buộc
10. Không bao giờ commit client secret hay token thật
```
