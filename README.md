# SSOExample - ví dụ SSO trên .NET 10

Repo này minh họa một hệ thống SSO tối giản gồm hai ứng dụng tách riêng:

1. **Back end API** chạy trên **.NET 10** (`src/SsoExample.Api`). API validate access token và cung cấp endpoint nghiệp vụ.
2. **Web client** chạy trên **.NET 10** (`src/SsoExample.Web`). Web host **jQuery SPA**, giữ cấu hình client-side của Microsoft Entra ID và gọi API bằng bearer token.

> Lưu ý: đây là code demo để giải thích kiến trúc. Khi production cần thay in-memory store bằng database thật, dùng HTTPS bắt buộc, xoay vòng key, harden cookie/token, rate limit và MFA.

## Chạy thử

Chạy API:

```bash
dotnet run --project src/SsoExample.Api/SsoExample.Api.csproj --launch-profile https
```

Chạy Web ở terminal khác:

```bash
dotnet run --project src/SsoExample.Web/SsoExample.Web.csproj --launch-profile https
```

Sau đó mở:

- API/static demo cũ: `https://localhost:5001/`
- Web .NET 10 host jQuery SPA: `https://localhost:5002/`

Tài khoản demo của local JWT flow:

| User | Password | Role |
| --- | --- | --- |
| `admin` | `Admin@123` | `Admin` |
| `alice` | `Alice@123` | `User` |
| `bob` | `Bob@123` | `User`, `Support` |

## Endpoint chính

| Endpoint | Mục đích |
| --- | --- |
| `POST /api/auth/login/password` | Login trực tiếp bằng username/password trong demo local JWT. |
| `GET /api/sso/authorize` | Bắt đầu flow SSO Authorization Code + PKCE dạng demo. |
| `POST /api/sso/token` | Đổi authorization code lấy access token/refresh token. |
| `POST /api/auth/token/refresh` | Refresh access token. |
| `POST /api/auth/login-as` | Backdoor có kiểm soát để admin impersonate user. |
| `GET /api/me` | Kiểm tra principal hiện tại. |
| `GET /api/orders` | API nghiệp vụ cần bearer token. |
| `GET /api/admin/users` | Admin xem user để chọn login-as. |
| `GET /api/admin/audit-logs` | Admin xem audit trail. |

## Cấu hình Microsoft Entra ID

Repo đã tách rõ hai appsettings theo đúng hai ứng dụng:

- API app registration tên Azure đề xuất: `SSOExample.Api`, cấu hình tại `src/SsoExample.Api/appsettings.json`.
- Web app registration tên Azure đề xuất: `SSOExample.Web`, cấu hình tại `src/SsoExample.Web/appsettings.json`.

Tóm tắt trách nhiệm:

| App | Appsettings giữ gì? |
| --- | --- |
| `SsoExample.Api` | Tenant, authority, API app `ClientId`, `ApplicationIdUri`, `Audience`, scope `access_as_user`, danh sách client app được phép gọi API. |
| `SsoExample.Web` | Tenant, authority, Web app `ClientId`, redirect URI, post logout URI, scopes cần xin, API base URL, local demo client ID và API scope cần attach khi gọi backend. |

Xem chi tiết mục **Appsettings và các bước tạo Microsoft Entra ID apps** trong [Thiết kế SSO](docs/sso-design.md).

## Tài liệu

- [Thiết kế SSO, flow, diagram và database](docs/sso-design.md)
- [DDL tham khảo](docs/database-schema.sql)
