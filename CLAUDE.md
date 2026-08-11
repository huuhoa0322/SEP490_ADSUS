# ADSUS — AI Automated Detection and Segmentation of Abnormal Structures Based on Ultrasound Images and Clinical Information

> Dự án capstone SEP490 - Hệ thống hỗ trợ chẩn đoán siêu âm tử cung bằng AI

---

## Tổng quan

**ADSUS** là hệ thống hỗ trợ chẩn đoán siêu âm tử cung bằng AI, kết nối 4 nhóm người dùng:
- **Web Admin**: Quản trị hệ thống, tài khoản, cấu hình AI
- **Web/Portal Bác sĩ**: Công cụ chuyên môn cho bác sĩ và điều dưỡng
- **App Mobile Bệnh nhân**: Theo dõi lịch khám, đơn thuốc, sức khỏe
- **Nghiên cứu AI**: Mô hình YOLO, đánh giá KPI

---

## Cấu trúc Project

```
SEP490_ADSUS/
├── ADSUS_BE/                      # ASP.NET Core 8.0 API (Backend chính)
├── ADSUS_BE.BLL/                   # Business Logic Layer
├── ADSUS_BE.DAL/                    # Data Access Layer
├── ADSUS_BE.UnitTests/              # Unit Tests (xUnit + Moq)
├── ADSUS_BE.IntegrationTests/       # Integration Tests
├── ADSUS_AIBE/                     # Python/FastAPI AI Backend (YOLO)
├── adsus-fe/                       # Next.js 16 Frontend (Admin + Doctor Portal)
├── ADSUS_Mobile/                   # Flutter Mobile App (Patient)
└── ADSUS_Documents-master/         # Documentation (nguồn: ADSUS_Documents-master)
```

---

## Kiến trúc hệ thống

```
┌─────────────────┐     ┌─────────────────┐
│   ADSUS_Mobile   │     │    adsus-fe     │
│   (Flutter)      │     │   (Next.js)     │
└────────┬─────────┘     └────────┬────────┘
         │                        │
         └──────────┬──────────────┘
                    │ HTTP/REST (JWT)
                    ▼
         ┌─────────────────────┐
         │     ADSUS_BE        │
         │  (ASP.NET Core 8)   │
         │   BLL → DAL         │
         └──────────┬──────────┘
                    │
    ┌───────────────┼───────────────┐
    │               │               │
    ▼               ▼               ▼
┌────────┐   ┌──────────┐   ┌──────────────┐
│PostgreSQL│  │ Supabase │   │ ADSUS_AIBE   │
│(Supabase│  │ Storage  │   │  (Python)     │
│ DB)     │  │ (Images) │   │  FastAPI+YOLO │
└────────┘   └──────────┘   └──────────────┘
```

---

## 4 Vai trò người dùng (Roles)

| Role | Nền tảng | Quyền chính |
|------|----------|-------------|
| **Admin** | Web Admin | Quản lý tài khoản, phân quyền, cấu hình AI, dashboard, blog |
| **Doctor** | Web Portal | Tra cứu hồ sơ, chạy AI, duyệt kết quả AI, kê đơn |
| **Nurse** | Web Portal | Tạo hồ sơ, tạo case, quản lý lịch khám (KHÔNG chạy/duyệt AI, KHÔNG kê đơn) |
| **Patient** | Mobile App | Xem hồ sơ, lịch khám, đơn thuốc, tuân thủ điều trị |

> **Quan trọng:** Nurse là vai trò ĐỘC LẬP với Doctor, không kế thừa quyền. Chỉ Doctor mới được chạy/duyệt AI và kê đơn.

---

## Module Backend (ADSUS_BE)

### 10 Module nghiệp vụ

| Module | Namespace | Mô tả |
|--------|----------|-------|
| **Auth** | `ADSUS_BE.BLL.Auth` | Đăng nhập, JWT, đổi mật khẩu, sinh trắc học (Mobile) |
| **UserRoleManagement** | `ADSUS_BE.BLL.UserRoleManagement` | CRUD tài khoản, phân quyền, audit log, quên mật khẩu (Email) |
| **DashboardReporting** | `ADSUS_BE.BLL.DashboardReporting` | Thống kê dashboard Admin |
| **MedicalRecord** | `ADSUS_BE.BLL.MedicalRecord` | Hồ sơ bệnh nhân, ca bệnh, ảnh siêu âm, chẩn đoán |
| **AIModelManagement** | `ADSUS_BE.BLL.AIModelManagement` | Quản lý phiên bản mô hình AI (HuggingFace) |
| **PrescriptionAdherence** | `ADSUS_BE.BLL.PrescriptionAdherence` | Kê đơn, nhắc uống thuốc, theo dõi tuân thủ |
| **AppointmentScheduling** | `ADSUS_BE.BLL.AppointmentScheduling` | Quản lý lịch khám, slot |
| **Engagement** | `ADSUS_BE.BLL.Engagement` | Blog, feedback (chat AI - tương lai) |
| **HealthMonitoring** | `ADSUS_BE.BLL.HealthMonitoring` | Theo dõi sức khỏe (Mobile) |
| **AIDiagnosis** | `ADSUS_BE.BLL.AIDiagnosis` | Gọi AI Backend, xử lý kết quả |

### Cấu trúc BLL mỗi module

```
Services/           # Logic nghiệp vụ
DTOs/               # Request/Response objects
Interfaces/         # Service interfaces (để mock khi test)
Validators/         # FluentValidation rules
```

---

## Công nghệ Stack

### Backend (.NET)
- **.NET 8.0 LTS**
- **ASP.NET Core Web API**
- **Entity Framework Core 8** + Npgsql (PostgreSQL)
- **FluentValidation** (validation)
- **Quartz.NET** (background jobs - nhắc uống thuốc)
- **Serilog** (logging)
- **BCrypt.Net** (password hashing)
- **Firebase Admin SDK** (Push notification - tương lai)

### Frontend (Next.js)
- **Next.js 16.2.11** (App Router)
- **React 19**
- **TypeScript**
- **Tailwind CSS 4** + **shadcn/ui** (Radix UI)
- **TanStack Query** (server state)
- **Zustand** (client state)
- **Axios** (HTTP client)

### Mobile (Flutter)
- **Flutter** (Patient App)
- **Riverpod** (state management)
- **Dio** (HTTP client)
- **flutter_secure_storage** (JWT storage)

### AI Backend (Python)
- **FastAPI**
- **Ultralytics YOLO**
- **HuggingFace Hub** (model download)
- **Pillow + NumPy**

### Database & Storage
- **PostgreSQL** (Supabase managed)
- **Supabase Storage** (ảnh siêu âm)

---

## Qui ước đặt tên

| Layer | Convention | Ví dụ |
|-------|------------|-------|
| .NET Backend | PascalCase | `AppointmentScheduling`, `CreateCaseRequest` |
| C# Classes | PascalCase | `CaseService`, `UserRepository` |
| C# Methods | PascalCase | `GetByIdAsync`, `CreateAsync` |
| TypeScript/FE | camelCase (vars) / kebab-case (files) | `useAppointments.ts`, `schedule-slot-management.tsx` |
| Flutter/Mobile | snake_case | `appointment_repository.dart`, `case_service.dart` |
| API Routes | kebab-case | `/api/ai-models`, `/schedule-slots` |

---

## API Endpoints chính

### Authentication
- `POST /api/auth/login` - Đăng nhập (số điện thoại + mật khẩu)
- `POST /api/auth/change-password` - Đổi mật khẩu
- `POST /api/auth/forgot-password` - Quên mật khẩu (Email)
- `POST /api/auth/refresh-token` - Refresh JWT

### User Management (Admin)
- `GET/POST /api/users` - Danh sách / Tạo tài khoản
- `GET/PUT/DELETE /api/users/{id}` - CRUD chi tiết

### Medical Records
- `GET/POST /api/patients/profiles` - Hồ sơ bệnh nhân
- `GET/POST /api/cases` - Ca bệnh
- `POST /api/cases/{id}/upload-image` - Upload ảnh siêu âm
- `POST /api/ai/run-diagnosis` - Gọi AI chẩn đoán

### Prescriptions
- `GET/POST /api/prescriptions` - Đơn thuốc
- `GET /api/medication-intakes` - Nhật ký uống thuốc
- `PUT /api/medication-intakes/{id}` - Cập nhật đã uống/chưa

### Appointments
- `GET/POST /api/schedule-slots` - Slot lịch khám
- `GET/POST /api/appointments` - Lịch hẹn

### AI Model Management
- `GET/POST /api/ai-models` - Phiên bản AI
- `POST /api/ai-models/{id}/activate` - Kích hoạt model

---

## Cấu hình quan trọng

### Backend (User Secrets - Development)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=...;Username=...;Password=..."
  },
  "JwtSettings": {
    "SecretKey": "...",
    "Issuer": "ADSUS",
    "Audience": "ADSUS-Users",
    "ExpiryMinutes": 60
  },
  "AiBackend": {
    "BaseUrl": "http://localhost:8000",
    "WebhookToken": "..."
  },
  "SupabaseStorage": {
    "Url": "https://xxx.supabase.co/storage/v1",
    "ServiceKey": "..."
  },
  "Email": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "...",
    "Password": "..."
  }
}
```

### Frontend (.env.local)

```env
NEXT_PUBLIC_API_URL=http://localhost:5000/api
```

---

## Build & Run

### Backend
```bash
cd ADSUS_BE
dotnet restore ADSUS_BE/ADSUS_BE.slnx
dotnet build ADSUS_BE/ADSUS_BE.slnx
dotnet run --project ADSUS_BE
```

### Frontend
```bash
cd adsus-fe
npm install
npm run dev
```

### AI Backend
```bash
cd ADSUS_AIBE
pip install -r backend/requirements.txt
cd backend
uvicorn main:app --reload --port 8000
```

---

## Testing

- **Unit Tests**: `ADSUS_BE.UnitTests/` - xUnit + Moq
- **Integration Tests**: `ADSUS_BE.IntegrationTests/` - WebApplicationFactory + Respawn

---

## Liên quan đến Documentation

Tài liệu chi tiết nằm trong thư mục song song: `ADSUS_Documents-master/`

- **UCS**: Đặc tả Use Case
- **TDS**: Technical Design Specification
- **PRD**: Product Requirements Document
- **ERD**: Entity Relationship Diagram
- **State Machine Diagrams**: Trạng thái các thực thể chính
- **Screenshots**: UI mockups

---

## Nguyên tắc làm việc

1. **AI không thay thế bác sĩ** - Kết quả AI luôn cần bác sĩ duyệt
2. **Admin không truy cập dữ liệu bệnh nhân** - Tách biệt hoàn toàn
3. **4 vai trò độc lập** - Không kế thừa quyền
4. **1 business logic cho cả 2 nền tảng** - Web và Mobile dùng chung API
5. **Số điện thoại là định danh DUY NHẤT** - Không còn username riêng
6. **Comment gắn UC-ID** - Khi sửa code, gắn comment tham chiếu UC (ví dụ `// UC-15`)

---

## Tiếp theo

Xem [architecture_guide.md](architecture_guide.md) để hiểu chi tiết luồng phát hiện AI và xác nhận của bác sĩ.
