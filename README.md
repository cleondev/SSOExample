# SSOExample — Demo SSO trên .NET 10

Repo này minh hoạ một hệ thống SSO tối giản gồm **hai project** chạy tách biệt:

| Project | Vai trò | Cổng dev |
| --- | --- | --- |
| `src/SsoExample.Api` | Resource API .NET 10: validate access token, expose endpoint nghiệp vụ, audit log. | `https://localhost:5001` |
| `src/SsoExample.Web` | Web .NET 10 host **jQuery SPA**, chạy flow Authorization Code + PKCE và gọi API bằng bearer token. | `https://localhost:5002` |

> Demo này dùng để giải thích kiến trúc. Khi production cần thay in-memory store bằng DB thật, dùng `Microsoft.Identity.Web` để validate token Entra ID, xoay vòng key, hash password Argon2id/bcrypt, MFA, rate limit và HTTPS bắt buộc.

## Mục lục

- [Quickstart](#quickstart)
- [UI và route của SPA](#ui-và-route-của-spa)
- [Endpoint chính của API](#endpoint-chính-của-api)
- [Cấu trúc thư mục](#cấu-trúc-thư-mục)
- [Cấu hình Microsoft Entra ID](#cấu-hình-microsoft-entra-id)
- [Tài liệu](#tài-liệu)

## Quickstart

```bash
# Trust dev cert (chỉ chạy 1 lần)
dotnet dev-certs https --trust

# Terminal 1 — API
dotnet run --project src/SsoExample.Api/SsoExample.Api.csproj --launch-profile https

# Terminal 2 — Web
dotnet run --project src/SsoExample.Web/SsoExample.Web.csproj --launch-profile https
```

Mở:

- Web SPA: <https://localhost:5002/>
- API dev portal: <https://localhost:5001/>

Tài khoản demo cho local JWT flow:

| User | Password | Role |
| --- | --- | --- |
| `admin` | `Admin@123` | `Admin` |
| `alice` | `Alice@123` | `User` |
| `bob` | `Bob@123` | `User`, `Support` |

## UI và route của SPA

SPA dùng hash-router, mọi view bind vào một `index.html` duy nhất:

| Route | Mô tả |
| --- | --- |
| `#/` | Landing: giới thiệu kiến trúc + list endpoint |
| `#/signin` | 2 card sign-in: **Sign in with Microsoft** (PKCE) + local password (chỉ dev) |
| `#/dashboard` | Hiển thị `/api/me`, danh sách `/api/orders`, decode access token |
| `#/admin` | Chỉ role `Admin`: users, audit log, form **login-as** |
| `#/about` | Diễn giải flow PKCE và link tới spec |

Banner đỏ luôn cố định trên đầu trang khi đang impersonate, kèm nút **Kết thúc login-as**.

## Endpoint chính của API

| Endpoint | Mục đích |
| --- | --- |
| `GET /api/health` | Liveness/runtime info, dùng cho landing API. |
| `GET /api/info` | Provider/authority/audience/scope hiện cấu hình. |
| `POST /api/auth/login/password` | Login local bằng username/password (demo). |
| `GET /api/sso/authorize` | Bắt đầu Authorization Code + PKCE (demo). |
| `POST /api/sso/token` | Đổi `code` + `code_verifier` lấy access/refresh token. |
| `POST /api/auth/token/refresh` | Refresh access token (rotation). |
| `POST /api/auth/login-as` | Backdoor có kiểm soát — admin impersonate user. |
| `GET /api/me` | Trả principal hiện tại. |
| `GET /api/orders` | Endpoint nghiệp vụ ví dụ, cần bearer token. |
| `GET /api/admin/users` | Danh sách user (chỉ Admin). |
| `GET /api/admin/audit-logs` | Audit log (chỉ Admin). |

Endpoint được tách module trong `src/SsoExample.Api/Endpoints/`:

- `AuthEndpoints.cs` — login/refresh/login-as
- `SsoEndpoints.cs` — authorize/token
- `BusinessEndpoints.cs` — `/api/me`, `/api/orders`
- `AdminEndpoints.cs` — users, audit log

## Cấu trúc thư mục

```
SSOExample/
├─ docs/
│  ├─ sso-design.md         # Kiến trúc, flow diagram, claims, DB
│  ├─ adopting-sso.md       # Hướng dẫn fresher port SSO sang site mới
│  └─ database-schema.sql   # DDL tham chiếu
├─ src/
│  ├─ SsoExample.Api/
│  │  ├─ Program.cs         # DI + middleware + wire endpoint groups
│  │  ├─ Endpoints/         # Auth / Sso / Business / Admin
│  │  ├─ Security/          # TokenService, PasswordHasher (demo only)
│  │  ├─ Data/              # InMemorySsoStore
│  │  ├─ Models/            # DTO/record
│  │  ├─ wwwroot/           # Dev portal landing
│  │  └─ appsettings.*.json # Required (SSO) + Optional (local demo)
│  └─ SsoExample.Web/
│     ├─ Program.cs         # Static host + /config + /health
│     ├─ wwwroot/
│     │  ├─ index.html      # Shell + templates 4 view
│     │  ├─ app.js          # Router, PKCE, session, view handlers
│     │  └─ site.css        # Design system
│     └─ appsettings.*.json
└─ SSOExample.sln
```

## Cấu hình Microsoft Entra ID

Mỗi project có **hai file appsettings** để tách rõ "bắt buộc cho SSO" và "optional cho local demo":

| App | Required giữ gì? | Optional giữ gì? |
| --- | --- | --- |
| `SsoExample.Api` | Provider, tenant/authority, API `ClientId`, `ApplicationIdUri`, `Audience`, scope `access_as_user`, danh sách client app được phép gọi API. | Local JWT demo (`Sso:*`), display name tenant/app, `AllowedHosts`. |
| `SsoExample.Web` | Tenant/authority, Web `ClientId`, API base URL, `Audience`, `RequiredScope`. | Redirect URIs, scope list cho MSAL, post-logout, cache location, demo client ID, display name. |

Tóm tắt mapping Azure → appsettings nằm trong `docs/sso-design.md` (mục 11) và đi sâu hơn — kèm bước Azure Portal — trong [`docs/adopting-sso.md`](docs/adopting-sso.md).

## Tài liệu

- [`docs/sso-design.md`](docs/sso-design.md) — kiến trúc, flow diagram, claims, DB.
- [`docs/adopting-sso.md`](docs/adopting-sso.md) — **hướng dẫn fresher** triển khai SSO cho một site khác từ con số 0.
- [`docs/database-schema.sql`](docs/database-schema.sql) — DDL tham khảo cho `users`, `roles`, `auth_codes`, `refresh_tokens`, `impersonation_sessions`, `audit_logs`.
