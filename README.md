# SSOExample - ví dụ SSO trên .NET 10

Repo này minh họa một hệ thống SSO tối giản gồm hai thành phần:

1. **Back end API** chạy trên **.NET 10** (`src/SsoExample.Api`).
2. **Client SPA** gồm **jQuery SPA** và **React SPA** được host chung trong `wwwroot` của API để dễ chạy demo.

> Lưu ý: đây là code demo để giải thích kiến trúc. Khi production cần thay in-memory store bằng database thật, dùng HTTPS bắt buộc, xoay vòng key, harden cookie/token, rate limit và MFA.

## Chạy thử

```bash
dotnet run --project src/SsoExample.Api/SsoExample.Api.csproj
```

Sau đó mở:

- `https://localhost:5001/jquery-spa/`
- `https://localhost:5001/react-spa/`

Tài khoản demo:

| User | Password | Role |
| --- | --- | --- |
| `admin` | `Admin@123` | `Admin` |
| `alice` | `Alice@123` | `User` |
| `bob` | `Bob@123` | `User`, `Support` |

## Endpoint chính

| Endpoint | Mục đích |
| --- | --- |
| `POST /api/auth/login/password` | Login trực tiếp bằng username/password. |
| `GET /api/sso/authorize` | Bắt đầu flow SSO Authorization Code + PKCE dạng demo. |
| `POST /api/sso/token` | Đổi authorization code lấy access token/refresh token. |
| `POST /api/auth/token/refresh` | Refresh access token. |
| `POST /api/auth/login-as` | Backdoor có kiểm soát để admin impersonate user. |
| `GET /api/me` | Kiểm tra principal hiện tại. |
| `GET /api/orders` | API nghiệp vụ cần bearer token. |
| `GET /api/admin/users` | Admin xem user để chọn login-as. |
| `GET /api/admin/audit-logs` | Admin xem audit trail. |

## Tài liệu

- [Thiết kế SSO, flow, diagram và database](docs/sso-design.md)
- [DDL tham khảo](docs/database-schema.sql)
