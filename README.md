# SSOExample

Demo SSO trên .NET 10 với **Microsoft Entra ID thật**. Hai project:

| Project | Vai trò | Cổng dev |
| --- | --- | --- |
| `src/SsoExample.Api` | Resource API: validate access token (Microsoft Entra hoặc local), expose nghiệp vụ, audit log. Swagger UI ở `/`. | `https://localhost:5001` |
| `src/SsoExample.Web` | jQuery SPA: Authorization Code + PKCE thật với Microsoft Entra ID. | `https://localhost:5002` |

## Quickstart

1. Cấu hình Microsoft Entra ID theo [docs/azure-setup.md](docs/azure-setup.md) — fill 13 trường Required (7 API + 6 Web). API và SPA fail-fast nếu còn placeholder.
2. Trust dev cert (1 lần): `dotnet dev-certs https --trust`
3. Run cả 2 project:

```bash
dotnet run --project src/SsoExample.Api --launch-profile demo
dotnet run --project src/SsoExample.Web --launch-profile demo
```

Mở `https://localhost:5002/` để vào SPA, `https://localhost:5001/` cho Swagger UI.

## Launch profile

| Profile | Environment | Appsettings được load |
| --- | --- | --- |
| `demo` | `Development` | Required + Optional. Local JWT (password login + login-as) cộng thêm Microsoft Entra. |
| `production` | `Production` | Chỉ Required. Microsoft Entra duy nhất. |

## Sign-in flow

1. SPA bấm **Sign in with Microsoft** → redirect tới `{authority}/oauth2/v2.0/authorize` với PKCE.
2. Microsoft Entra ID đăng nhập user, redirect về `/auth/callback?code=...&state=...`.
3. SPA POST `{authority}/oauth2/v2.0/token` với `code` + `code_verifier` → nhận `access_token` + `id_token`.
4. SPA gọi API với `Authorization: Bearer <access_token>`. API validate qua JwtBearer middleware (issuer + audience + `azp` ∈ AllowedClientApplications + signature).

Password login + login-as (legacy demo) vẫn hoạt động song song, dùng local-signed JWT cho mục đích minh hoạ impersonation.

## Tài khoản demo (chỉ cho password login)

| User | Password | Role |
| --- | --- | --- |
| `admin` | `Admin@123` | `Admin` |
| `alice` | `Alice@123` | `User` |
| `bob` | `Bob@123` | `User`, `Support` |

## Route SPA

| Hash | Mô tả |
| --- | --- |
| `#/signin` | Sign in with Microsoft + password form (demo). Default khi chưa đăng nhập. |
| `#/dashboard` | Principal hiện tại + `/api/orders` + decode token. |
| `#/admin` | Users grid (kèm action Login as), audit log auth events. |
| `#/audit-history` | Request log: ai gọi endpoint nào (Admin only). |

## Endpoint API

| Endpoint | Quyền | Mục đích |
| --- | --- | --- |
| `GET /api/health`, `/api/info` | anon | Probe + cấu hình hiện tại. |
| `POST /api/auth/login/password` | anon | Local password login (demo). |
| `POST /api/auth/token/refresh` | anon | Refresh local-issued token. |
| `POST /api/auth/login-as` | Admin | Impersonate user, issue local-signed JWT. Reason ≥ 10 ký tự. |
| `GET /api/me`, `/api/orders` | bearer | Principal + nghiệp vụ. |
| `GET /api/admin/users`, `/audit-logs`, `/request-logs` | Admin | Quản trị + audit. |

Swagger UI tại `https://localhost:5001/` cho schema và Try it out.

## Cấu hình

- `appsettings.Required.json` — **bắt buộc** cho Microsoft Entra ID (tenant, client ID, audience, scope, allowed clients). Fail-fast nếu placeholder.
- `appsettings.Optional.json` — `Sso:*` (signing key cho local impersonation token), `RedirectUris`, `LocalDemoClientId`, `AllowedHosts`.

Setup Azure step-by-step: [docs/azure-setup.md](docs/azure-setup.md).
