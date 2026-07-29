# Module 1 — Xác thực & Tài khoản: Hướng dẫn chạy và kiểm thử

Module 1 đã merge vào `master` (PR #5). Tài liệu này hướng dẫn cả nhóm chạy thử,
và quan trọng hơn — chỉ cách dùng lại hạ tầng xác thực cho module của mình.

**Phạm vi đã làm:** FT-01 Đăng nhập, FT-02 Đăng xuất, FT-05 Đổi mật khẩu — **chỉ nền Web**.
FT-03 (vân tay) và FT-04 (hồ sơ cá nhân) là Mobile, chưa làm.

---

## 1. Chuẩn bị máy (làm một lần)

### 1.1 Lấy code mới nhất

```bash
git checkout master
git pull origin master
```

### 1.2 Cấu hình User Secrets — **bắt buộc, không có là không chạy được**

Chuột phải project **ADSUS_BE** trong Visual Studio → **Manage User Secrets**.
Dán nội dung sau, **lấy giá trị thật trong nhóm Zalo**:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<chuỗi kết nối Supabase — hỏi Zalo>"
  },
  "JwtSettings": {
    "SecretKey": "<khoá JWT — hỏi Zalo>",
    "Issuer": "ADSUS_BE",
    "Audience": "ADSUS_Client",
    "ExpiryMinutes": 60
  }
}
```

> ⚠️ **`SecretKey` phải giống hệt nhau trên mọi máy.** Ai tự sinh khoá riêng thì token
> máy này máy kia không đọc được — sẽ dính lỗi 401 rất khó lần ra nguyên nhân.

> ⚠️ Không bao giờ đưa hai giá trị này vào `appsettings.json` — file đó được commit lên Git.

Nếu thiếu `JwtSettings`, app sẽ **dừng ngay lúc khởi động** kèm thông báo hướng dẫn,
chứ không để lỗi khó hiểu xuất hiện lúc gọi API.

### 1.3 Cài Node.js (chỉ cần nếu muốn chạy giao diện)

Cài **Node.js LTS** (>= 20) từ [nodejs.org](https://nodejs.org), rồi:

```bash
cd adsus-fe
npm install
```

---

## 2. Chạy

### Backend

Bấm **F5** trong Visual Studio, hoặc:

```bash
cd ADSUS_BE
dotnet run --launch-profile http
```

Chạy ở **http://localhost:5036** — Swagger tại `/swagger`.

### Frontend

```bash
cd adsus-fe
npm run dev
```

Chạy ở **http://localhost:3000** — màn đăng nhập tại `/login`.

> Muốn thử giao diện thì **phải chạy cả hai**. Backend không chạy thì frontend sẽ báo
> "Không kết nối được tới máy chủ".

---

## 3. Tài khoản kiểm thử

Đã seed sẵn trong DB. **Mật khẩu chung: `Test@123`**

| Số điện thoại | Vai trò | Trạng thái | Dùng để kiểm |
| :--- | :--- | :--- | :--- |
| `0900000001` | ADMIN | Active | Đăng nhập được → vào `/dashboard` |
| `0900000002` | DOCTOR | Active | Đăng nhập được → vào `/patients` |
| `0900000003` | PATIENT | Active | Đăng nhập được (thực tế Patient dùng Mobile) |
| `0900000004` | DOCTOR | **Locked** | **Phải bị từ chối** dù mật khẩu đúng |
| `0900000005` | DOCTOR | **Deactivated** | **Phải bị từ chối** dù mật khẩu đúng |
| `0900000006` | DOCTOR | Active | Bị **ép đổi mật khẩu** trước khi vào đâu khác |

Muốn xoá hết tài khoản test:

```sql
DELETE FROM public.users WHERE phone LIKE '09000000%';
```

---

## 4. Kịch bản kiểm thử

### 4.1 Trên giao diện (http://localhost:3000/login)

| # | Thao tác | Kết quả đúng |
| :--- | :--- | :--- |
| 1 | Đăng nhập `0900000001` / `Test@123` | Vào `/dashboard`, header hiện "TEST Admin — Quản trị viên" |
| 2 | Đăng nhập `0900000002` / `Test@123` | Vào `/patients` |
| 3 | Đăng nhập sai mật khẩu | Báo *"Số điện thoại hoặc mật khẩu không đúng."* |
| 4 | Đăng nhập số không tồn tại | Báo **y hệt** câu ở dòng 3 |
| 5 | Đăng nhập `0900000004` (bị khoá) | Báo **y hệt** câu ở dòng 3 |
| 6 | Đăng nhập `0900000005` (vô hiệu hoá) | Báo **y hệt** câu ở dòng 3 |
| 7 | Đăng nhập `0900000006` | Bị đưa thẳng sang `/change-password`, **không** vào `/patients` |
| 8 | Đang ở bước 7, gõ tay `/dashboard` | Bị đẩy ngược về `/change-password` |
| 9 | Chưa đăng nhập, gõ tay `/dashboard` | Bị đẩy về `/login` |
| 10 | Bấm **Đổi mật khẩu** trên header | Mở màn SCR-04, có checklist yêu cầu mật khẩu |
| 11 | Nhập sai mật khẩu hiện tại | Báo *"Mật khẩu hiện tại không đúng."* |
| 12 | Gõ mật khẩu mới quá ngắn / thiếu chữ hoa / thiếu số | Checklist tự đổi màu theo từng mục |
| 13 | Bấm **Đăng xuất** | Về `/login`, token bị xoá sạch |

**Dòng 3–6 là điểm quan trọng nhất.** UCS quy tắc GB-06 bắt buộc mọi lỗi đăng nhập phải
hiện **thông báo giống hệt nhau**, không được để lộ nguyên nhân thật. Nếu thấy 4 trường
hợp này ra 4 câu khác nhau là **lỗi bảo mật**, báo ngay.

### 4.2 Trên Swagger (http://localhost:5036/swagger)

**Đăng nhập:**
`POST /api/v1/auth/login` → *Try it out* →
```json
{ "phoneNumber": "0900000001", "password": "Test@123" }
```

**Thử endpoint cần đăng nhập:**
1. Copy `accessToken` từ response trên
2. Bấm nút **Authorize** góc phải trên
3. Dán token vào (không cần gõ chữ `Bearer`)
4. Gọi `POST /api/v1/auth/change-password`

---

## 5. Dùng lại hạ tầng cho module của bạn

### 5.1 Backend — bảo vệ endpoint

Từ giờ dùng được ngay, không phải cấu hình thêm gì:

```csharp
[Authorize]                              // bắt buộc đăng nhập
[Authorize(Roles = "ADMIN")]             // chỉ Admin
[Authorize(Roles = "ADMIN,DOCTOR")]      // Admin hoặc Doctor
```

Lấy thông tin người đang đăng nhập trong Controller:

```csharp
using System.Security.Claims;

var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
var phone  = User.FindFirstValue(ClaimTypes.MobilePhone);
var role   = User.FindFirstValue(ClaimTypes.Role);   // "ADMIN" / "DOCTOR" / "PATIENT"
```

### 5.2 Backend — response phải theo mẫu chung

```csharp
return Ok(ApiResponse<MyDto>.Ok(data, "Thành công."));
return BadRequest(ApiResponse<MyDto>.Fail(400, "Thông báo lỗi."));
```

Nằm ở `ADSUS_BE.BLL/Common/ApiResponse.cs`.

### 5.3 Backend — enum PostgreSQL

Nếu module bạn dùng enum trong DB (`case_status`, `appointment_status`...), phải khai
báo hai chỗ, nếu không sẽ **lỗi lúc chạy dù build vẫn qua**:

```csharp
// 1. ADSUS_BE.DAL/Entities/Enums.cs — [PgName] khớp đúng nhãn viết hoa trong DB
public enum CaseStatus
{
    [PgName("CREATED")] Created,
    [PgName("ANALYZED")] Analyzed,
}

// 2. ADSUS_BE/Program.cs — thêm vào chỗ đã có sẵn MapEnum
dataSourceBuilder.MapEnum<CaseStatus>("case_status");
```

### 5.4 Frontend — gọi API

```ts
import { apiClient } from "@/lib/api-client";

// Token tự động được gắn vào header, không phải làm gì thêm
const { data } = await apiClient.get("/api/v1/patients");
```

### 5.5 Frontend — trang cần đăng nhập

Đặt file vào `src/app/(protected)/` là **tự động được bảo vệ**, không phải nhớ gắn gì:

```
src/app/(protected)/my-page/page.tsx    -> chưa đăng nhập sẽ bị đá về /login
```

### 5.6 Frontend — màu và font

Dùng biến có sẵn, **đừng tự chọn màu**, để 29 màn hình trông đồng nhất:

| Biến | Màu | Dùng cho |
| :--- | :--- | :--- |
| `bg-primary` / `text-primary` | navy `#223a66` | Màu chủ đạo, tiêu đề |
| `bg-accent` / `text-accent` | teal `#1cba9f` | Nút bấm, điểm nhấn |
| `text-destructive` | hồng `#f13a66` | Lỗi, cảnh báo |
| `font-heading` | Exo | Tiêu đề |
| (mặc định) | Roboto | Nội dung |

Bảng màu lấy từ template Medizco của nhóm.

---

## 6. Gặp lỗi thì xử lý thế nào

| Triệu chứng | Nguyên nhân | Cách sửa |
| :--- | :--- | :--- |
| App dừng ngay khi khởi động, báo thiếu `JwtSettings` | Chưa cấu hình User Secrets | Làm lại mục 1.2 |
| Mọi request đều 401 dù đã đăng nhập | Khoá JWT khác với người khác | Lấy lại đúng khoá trong Zalo |
| Trình duyệt báo lỗi CORS | Backend chưa chạy, hoặc chạy sai cổng | Backend phải ở cổng **5036** |
| FE báo "Không kết nối được tới máy chủ" | Backend chưa bật | Chạy backend trước |
| `npm` báo không tìm thấy lệnh | Chưa cài Node.js | Làm lại mục 1.3 |
| `FileNotFoundException` khi truy vấn DB | Thiếu `Microsoft.EntityFrameworkCore.Relational 8.0.29` | Đã sửa ở `master`, `git pull` là hết |
| Build được nhưng chạy lỗi ở cột enum | Quên `MapEnum` | Xem mục 5.3 |

---

## 7. Chạy unit test

```bash
dotnet test ADSUS_BE.UnitTests/ADSUS_BE.UnitTests.csproj
```

Hiện có **60 test** — 27 của Module 1, 33 của Module 7. Tất cả phải pass.

---

## 8. Những điều cần biết trước khi tin tưởng hệ thống

**Chặn truy cập ở frontend KHÔNG phải lớp bảo vệ dữ liệu.** Người dùng hoàn toàn có thể
sửa `localStorage` để lọt qua `AuthGuard`. Lớp bảo vệ thật nằm ở `[Authorize]` phía
backend — **module nào cũng phải tự gắn**, đừng dựa vào frontend.

**Chưa có gì chống dò mật khẩu.** Nhóm đã bỏ quy tắc BR-04 (tự khoá sau N lần sai), nên
hiện tại một script có thể thử mật khẩu vô hạn lần. Cần bàn lại trước khi triển khai thật.

**Token để trong `localStorage`** — dính lỗ hổng XSS là mất token. An toàn hơn là dùng
httpOnly cookie, nhưng phải sửa cả backend lẫn chống CSRF. Đây là hệ thống y tế nên nhóm
nên bàn lại.

**Vai trò NURSE chưa có trong database.** UCS định nghĩa 4 vai trò nhưng enum `user_role`
mới có 3. Cần `ALTER TYPE` trước khi làm phần liên quan tới Nurse.
