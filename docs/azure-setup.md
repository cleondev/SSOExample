# Azure setup cho SSOExample

Bắt buộc đọc trước khi chạy — API và SPA fail-fast nếu Required còn placeholder.

Cần điền **7 trường** trong `src/SsoExample.Api/appsettings.Required.json` và **6 trường** trong `src/SsoExample.Web/appsettings.Required.json`. Mọi trường đều là **mandatory** cho production.

## 0. Chuẩn bị

- Account Azure có quyền tạo App registration trên tenant của bạn.
- Mở Azure Portal → **Microsoft Entra ID** → **App registrations**.

## 1. Tạo `SSOExample.Api` (Resource API)

### 1.1. Đăng ký app

**App registrations → + New registration**

| Field | Giá trị |
| --- | --- |
| Name | `SSOExample.Api` |
| Supported account types | `Accounts in this organizational directory only (Single tenant)` |
| Redirect URI | (bỏ trống — API không cần) |

Bấm **Register**.

### 1.2. Copy 2 ID từ trang Overview

| Field trên Azure | Lưu lại để điền sau |
| --- | --- |
| **Directory (tenant) ID** | gọi tắt là `TENANT_ID` |
| **Application (client) ID** | gọi tắt là `API_CLIENT_ID` |

### 1.3. Expose an API → thêm scope

**Expose an API**

1. **Application ID URI** → **Add** → Azure điền sẵn `api://<API_CLIENT_ID>` → **Save** ngay (không sửa). Đây là format duy nhất luôn pass policy mặc định của tenant.
2. **+ Add a scope**:

   | Field | Giá trị |
   | --- | --- |
   | Scope name | `access_as_user` |
   | Who can consent | `Admins and users` |
   | Admin consent display name | `Access SSOExample API as signed-in user` |
   | Admin consent description | (tự đặt) |
   | State | `Enabled` |

Full scope name kết quả: `api://<API_CLIENT_ID>/access_as_user`.

### 1.4. App roles

**App roles → + Create app role** — tạo 3 role:

| Display name | Allowed member types | Value |
| --- | --- | --- |
| `Admin` | `Users/Groups` | `Admin` |
| `Support` | `Users/Groups` | `Support` |
| `User` | `Users/Groups` | `User` |

`Value` phải đúng casing (token claim `roles` phân biệt hoa thường).

## 2. Tạo `SSOExample.Web` (SPA client)

**App registrations → + New registration**

| Field | Giá trị |
| --- | --- |
| Name | `SSOExample.Web` |
| Supported account types | Single tenant (cùng tenant với API) |
| Redirect URI — platform | **Single-page application** |
| Redirect URI — URL | `https://localhost:5002/auth/callback` |

Bấm **Register**, copy **Application (client) ID** → gọi tắt `WEB_CLIENT_ID`.

### 2.1. Authentication — phải là Single-page application

**Authentication** → kéo xuống **Platform configurations**.

⚠️ Quan trọng: platform **phải là `Single-page application`**, KHÔNG phải `Web` hay `Mobile and desktop applications`. Sai platform → lỗi `AADSTS500113: No reply address is registered` (Web không cho phép PKCE không có secret) hoặc CORS chặn khi browser fetch `/oauth2/v2.0/token`.

Nếu lúc đăng ký không khai redirect URI hoặc khai sai platform:

1. **Platform configurations → + Add a platform → Single-page application**.
2. Nhập `https://localhost:5002/auth/callback` (chính xác, không thừa/thiếu trailing slash) → **Configure**.
3. Đợi 30-60 giây Azure propagate config.

Nếu đang có platform sai (Web/Mobile) trỏ cùng URI: bấm icon thùng rác để xóa platform đó rồi tạo lại với SPA.

Các setting còn lại:
- **Front-channel logout URL**: `https://localhost:5002/` (optional).
- **Implicit grant and hybrid flows**: KHÔNG tick.
- **Allow public client flows**: `No`.
- **Certificates & secrets**: KHÔNG tạo client secret — SPA là public client, secret rò ra browser sẽ bị abuse.

### 2.2. API permissions

**API permissions → + Add a permission**

1. **My APIs** → chọn `SSOExample.Api`.
2. **Delegated permissions** → tick `access_as_user` → **Add permissions**.
3. Bấm **Grant admin consent for <tenant>**. Status của `access_as_user` phải đổi sang **Granted for <tenant>** (dấu tick xanh).

## 3. Gán role cho user

**Microsoft Entra ID → Enterprise applications → SSOExample.Api → Users and groups → + Add user/group**

- Chọn user/group → chọn role (`Admin` / `Support` / `User`).
- Một user nhiều role → assign nhiều dòng.

User phải sign out & sign in lại để token chứa claim mới.

## 4. Điền vào appsettings

### 4.1. `src/SsoExample.Api/appsettings.Required.json`

| Field | Giá trị | Ghi chú |
| --- | --- | --- |
| `Authentication.MicrosoftEntraId.TenantId` | `TENANT_ID` | Copy từ §1.2. |
| `Authentication.MicrosoftEntraId.Authority` | `https://login.microsoftonline.com/TENANT_ID/v2.0` | Replace `TENANT_ID` bằng giá trị thật. |
| `Authentication.MicrosoftEntraId.Api.ClientId` | `API_CLIENT_ID` | Copy từ §1.2. |
| `Authentication.MicrosoftEntraId.Api.ApplicationIdUri` | `api://API_CLIENT_ID` | Y hệt §1.3. |
| `Authentication.MicrosoftEntraId.Api.Audience` | `api://API_CLIENT_ID` | Bằng `ApplicationIdUri`. API validate `aud` trong token bằng giá trị này. |
| `Authentication.MicrosoftEntraId.Api.Scopes.AccessAsUser` | `access_as_user` | Tên scope từ §1.3. |
| `Authentication.MicrosoftEntraId.AllowedClientApplications[0].ClientId` | `WEB_CLIENT_ID` | Web app được phép gọi API (§2). |

Ví dụ điền xong:

```json
{
  "Authentication": {
    "Provider": "MicrosoftEntraId",
    "MicrosoftEntraId": {
      "TenantId": "b3b40bc6-b026-4f9b-abec-ab701dfeec71",
      "Authority": "https://login.microsoftonline.com/b3b40bc6-b026-4f9b-abec-ab701dfeec71/v2.0",
      "Api": {
        "ClientId": "720539f2-d0f3-4064-8ecd-908cea89f791",
        "ApplicationIdUri": "api://720539f2-d0f3-4064-8ecd-908cea89f791",
        "Audience": "api://720539f2-d0f3-4064-8ecd-908cea89f791",
        "Scopes": { "AccessAsUser": "access_as_user" }
      },
      "AllowedClientApplications": [
        { "ClientId": "<WEB_CLIENT_ID-thật>" }
      ]
    }
  }
}
```

### 4.2. `src/SsoExample.Web/appsettings.Required.json`

| Field | Giá trị | Ghi chú |
| --- | --- | --- |
| `MicrosoftEntraId.TenantId` | `TENANT_ID` | Y hệt API. |
| `MicrosoftEntraId.Authority` | `https://login.microsoftonline.com/TENANT_ID/v2.0` | Y hệt API. |
| `MicrosoftEntraId.ClientId` | `WEB_CLIENT_ID` | Copy từ §2. |
| `Api.BaseUrl` | `https://localhost:5001` | URL API dev (đổi cho prod). |
| `Api.Audience` | `api://API_CLIENT_ID` | Y hệt API.Audience. SPA gửi scope tới Entra để xin token có audience này. |
| `Api.RequiredScope` | `api://API_CLIENT_ID/access_as_user` | Full scope SPA yêu cầu. |

### 4.3. `src/SsoExample.Web/appsettings.Optional.json`

```json
{
  "MicrosoftEntraId": {
    "RedirectUris": [ "https://localhost:5002/auth/callback" ]
  },
  "Api": {
    "LocalDemoClientId": "ssoexample-web"
  },
  "AllowedHosts": "*"
}
```

`RedirectUris[0]` là redirect_uri SPA gửi cho Entra ID — phải khớp **chính xác** với URI đã đăng ký ở §2. SPA tự build scope list từ `Api.RequiredScope` ở Required, không cần ghi lại ở Optional.

## 5. Cách code phát hiện cấu hình

- **API** (`Program.cs`): startup gọi `RequireConfig` cho mọi trường trong §4.1. Nếu một trường có ký tự `<` hoặc `>` → throw `InvalidOperationException`. App không start được — buộc bạn fill đủ.
- **SPA** (`app.js`): nút **Sign in with Microsoft** check `useEntraId()` (`authority` và `clientId` không có `<`). Nếu thiếu → button disabled, hiển thị hint yêu cầu cấu hình. Đã cấu hình → redirect `{base}/oauth2/v2.0/authorize` với `base = authority.replace('/v2.0','')` (Microsoft endpoint nằm ở `/oauth2/v2.0/...` không nằm dưới `/v2.0/`).

API ngoài JwtBearer validate Microsoft token còn check **`azp` claim** phải nằm trong `AllowedClientApplications`. Token đến từ client app khác sẽ bị từ chối 401.

## 6. Checklist

- [ ] `SSOExample.Api` có scope `access_as_user` và 3 app role `Admin/Support/User`.
- [ ] `SSOExample.Web` platform = SPA, redirect URI = `https://localhost:5002/auth/callback`.
- [ ] `SSOExample.Web` đã grant admin consent cho `access_as_user`.
- [ ] User/Group đã assign role trong Enterprise applications.
- [ ] 7 trường API Required + 6 trường Web Required đã điền giá trị thật, không còn `<...>`.
- [ ] `AllowedClientApplications[0].ClientId` của API = `WEB_CLIENT_ID`.

## 7. Lỗi hay gặp

| Triệu chứng | Nguyên nhân |
| --- | --- |
| API start báo `Configuration '...' chưa được cấu hình` | Một trường Required còn placeholder. Đọc message để biết key nào. |
| `Failed to add identifier URI ... must contain a tenant verified domain, tenant ID, or app ID` | Đã đổi App ID URI sang slug ngắn — giữ `api://<API_CLIENT_ID>` mặc định. |
| `AADSTS500113: No reply address is registered for the application` | `SSOExample.Web` chưa có Redirect URI nào, hoặc URI đăng ký dưới platform `Web`/`Mobile` thay vì `Single-page application`. Xem §2.1. |
| `AADSTS50011: redirect URI mismatch` | URI trong Azure khác URI Web đang gọi. Check http/https, port, trailing slash, casing — phải khớp byte-by-byte. |
| `AADSTS65001: consent_required` | Chưa grant admin consent cho `access_as_user`. |
| Browser console: CORS chặn khi POST `/oauth2/v2.0/token` | Redirect URI đăng ký nhầm platform `Web` — Microsoft chỉ trả CORS header cho platform `Single-page application`. Xóa và tạo lại với SPA. |
| SPA login xong gọi API trả 401 với message `Client app '...' không nằm trong AllowedClientApplications` | `azp` của token là Web client ID, nhưng API's `AllowedClientApplications` chưa có giá trị này. |
| SPA login xong gọi API trả 401 khác | `Audience` API Required khác `aud` trong token — decode ở jwt.ms để so. |
| Token có scope nhưng `roles` rỗng | User chưa assign role trong Enterprise applications, hoặc token còn cũ — sign out + sign in lại. |
| CORS preflight 401 | `UseCors` phải đặt trước `UseAuthentication` (đã đúng trong repo). |
