# Project: ADSUS Comprehensive End-to-End Testing, Audit & Performance Profiling

## Architecture
- **Web Frontend (`adsus-fe`)**: Next.js 15 App Router, TypeScript, TanStack Query, Tailwind CSS, Radix UI / shadcn/ui. 43 routes, 21 domain features, 284 interactive buttons.
- **Backend API (`ADSUS_BE`)**: ASP.NET Core 9 REST API, Entity Framework Core, PostgreSQL, JWT Authentication (HMAC-SHA256, 60min expiry), SignalR Hubs. 37 controllers, 165 endpoints.
- **Mobile App (`ADSUS_Mobile`)**: Flutter 3, Riverpod 2.6.1, GoRouter 17.3.0, DioClient, FlutterSecureStorage, Hive, native platform channels (Calendar, Deep Links). 191 passed unit/widget tests.
- **Database (`PostgreSQL`)**: Supabase managed PostgreSQL 17.6 pooler (and local PostgreSQL 18 service on port 5432). 42 tables, 96 indexes, 59 foreign keys, `pg_stat_statements` 1.11 profiling enabled.

## Feature Inventory
| # | Feature ID | Feature Name | Description | Assigned Milestone | Source |
|---|---|---|---|---|---|
| 1 | UC-01 | Đăng nhập hệ thống | Auth login with phone/password, password toggle, JWT generation | M1 | Survey Web & BE |
| 2 | UC-02 | Đăng ký tài khoản bệnh nhân | 3-step registration (phone, OTP verification, profile completion) | M1 | Survey Web & BE |
| 3 | UC-03 | Quên mật khẩu / Cấp lại | Self-service reset via phone/email, AF-01 ambiguity protection | M1 | Survey Web & BE |
| 4 | UC-04 | Quản trị tài khoản & nhân sự | Admin user management, role assignment, password reset, deactivate/reactivate | M1 | Survey Web & BE |
| 5 | UC-05 | Bảng điều khiển thống kê | Admin dashboard metrics, appointment & revenue statistics, date filtering | M1 | Survey Web & BE |
| 6 | UC-06 | Tiếp nhận tạo bệnh nhân tại quầy | Staff creating patient account, clinical history, temporary password issuance | M1 | Survey Web & BE |
| 7 | UC-07 | Quản lý người thân bệnh nhân | Adding/deleting relatives, relationship type tagging, phone verification | M1 | Survey Web & BE |
| 8 | UC-08 | Tiếp nhận & Check-in tại quầy | Nurse check-in queue, filtering, status transitions, ticket printing check | M1 | Survey Web & BE |
| 9 | UC-09 | Danh sách bệnh nhân lâm sàng | Doctor/Staff patient directory search, filtering by consultation status | M1 | Survey Web & BE |
| 10 | UC-10 | Hồ sơ bệnh án & Tiền sử bệnh | Patient medical record history, chronic diseases, allergy lists | M1 | Survey Web & BE |
| 11 | UC-11 | Chi tiết ca khám & Ghi nhận lâm sàng | Doctor clinical encounter, symptoms, ultrasound upload, AI analysis studio link | M1 | Survey Web & BE |
| 12 | UC-12 | Kết luận & Xuất báo cáo ca khám | RichText final diagnosis, saving, locking/confirming, PDF export | M1 | Survey Web & BE |
| 13 | UC-13 | Đặt lịch khám trực tuyến | Web & Staff appointment booking, doctor/date/slot selection, symptoms | M1 | Survey Web & BE |
| 14 | UC-14 | Quản lý & Hủy lịch hẹn | Web appointment management, reschedule, cancellation with reason | M1 | Survey Web & BE |
| 15 | UC-15 | Đổi lịch hẹn bởi Điều dưỡng | Staff rescheduling appointment in check-in modal with autoCheckin | M1 | Survey Web & BE |
| 16 | UC-16 | Bác sĩ sẵn sàng đón bệnh nhân | Doctor ready-next patient queue trigger, shift requests, overtime | M1 | Survey Web & BE |
| 17 | UC-17 | Phân tích siêu âm AI & Caliper | AI studio, caliper drawing (4 points), burnt image generation, confirmation | M1 | Survey Web & BE |
| 18 | UC-18 | Kê đơn thuốc & Hoàn tất ca khám | Doctor prescription form, drug search, dose scheduling, invoice generation | M1 | Survey Web & BE |
| 19 | UC-19 | Kết thúc ca không kê đơn | Doctor ending consultation without prescription, follow-up scheduling | M1 | Survey Web & BE |
| 20 | UC-20 | Quản lý danh mục thuốc | Medicine master catalog, active ingredient, packaging units, status toggle | M1 | Survey Web & BE |
| 21 | UC-21 | Quản lý lô thuốc & Điều chỉnh tồn kho | Drug batch tracking, lot numbers, expiry dates, inventory adjustments | M1 | Survey Web & BE |
| 22 | UC-22 | Nhập kho thuốc (Đơn lẻ & Bulk Excel) | Stock import form, supplier mapping, bulk Excel import, staging validation | M1 | Survey Web & BE |
| 23 | UC-23 | Cảnh báo tồn kho thuốc | Low stock and out-of-stock inventory alerts, direct re-order navigation | M1 | Survey Web & BE |
| 24 | UC-24 | Quản lý hóa đơn & Thanh toán FEFO | Invoice list, detail, cash/bank transfer payment, VietQR, FEFO drug deduction | M1 | Survey Web & BE |
| 25 | UC-25 | Tự đổi mật khẩu người dùng | Password change form with real-time policy validation checklist | M1 | Survey Web & BE |
| 26 | UC-26 | Quản lý & Duyệt ca nghỉ/tăng ca | Admin shift requests approval/rejection with mandatory rejection reason | M1 | Survey Web & BE |
| 27 | UC-27 | Quản lý dịch vụ phòng khám | Admin clinical services master CRUD, deactivate/reactivate | M1 | Survey Web & BE |
| 28 | UC-28 | Quản lý bài viết y khoa | Medical blog creation, rich content, draft, publish, public view | M1 | Survey Web & BE |
| 29 | UC-29 | Tra cứu nhật ký hệ thống (Audit Logs) | Admin audit log search, category filtering, date range, JSON viewer | M1 | Survey Web & BE |
| 30 | UC-30 | Theo dõi tuân thủ uống thuốc | Doctor monitoring patient drug adherence, custom reminder notification | M1 | Survey Web & BE |
| 31 | UC-31 | Quản lý mô hình AI & Metric mAP50 | AI model versions registration, active model switch, mAP50 evaluation | M1 | Survey Web & BE |
| 32 | UC-32 | Phản hồi & Đánh giá dịch vụ | Patient consultation feedback, star ratings, comments, admin feedback review | M1 | Survey Web & BE |
| 33 | MOB-01 | Mobile Auth & Profile | Biometric sign-in, phone/password, temporary password change, profile edit | M2 | Survey Mobile |
| 34 | MOB-02 | Mobile Appointment Booking | Doctor filter, week/date chips, slot pills grid, symptom accordion, self/relative | M2 | Survey Mobile |
| 35 | MOB-03 | Mobile MyAppointments & Calendar Sync | Segmented tabs (Tôi / Người thân), native OS Google/Apple Calendar sync | M2 | Survey Mobile |
| 36 | MOB-04 | Mobile 3rd Cancel Warning & Sheet | 3rd cancel limit warning popup, layout symmetry audit, cancel reason sheet | M2 | Survey Mobile |
| 37 | MOB-05 | Mobile AI Health Chatbot | Safety disclaimer (GB-02), intent detection badges, suggestion chips, deep links | M2 | Survey Mobile |
| 38 | PERF-01 | API Latency Benchmarking | Response time measurement across 17 candidate endpoints | M3 | Survey DB & Infra |
| 39 | PERF-02 | PostgreSQL Query & Index Audit | `pg_stat_statements` analysis, slow queries (>200ms), missing FK indexes | M3 | Survey DB & Infra |
| 40 | SEC-01 | Cross-Role RBAC Verification | 6-pair cross-role matrix testing 401 Unauthorized / 403 Forbidden | M4 | Survey DB & Infra |
| 41 | SEC-02 | IDOR Vulnerability Audit | Audit of `GET /api/v1/appointments/{id}` and ownership verification across API | M4 | Survey DB & Infra |
| 42 | CLN-01 | Test Data Cleanup & DB Restoration | Deterministic 18-step DAG cleanup script, verifying zero residual test records | M5 | Survey DB & Infra |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|---|---|---|---|
| M1 | Web Frontend & Backend Functional/Button Audit | Audit all 32 Web UCs, 284 buttons, dead buttons, ticket print gap, footer links | None | DONE |
| M2 | Mobile App Flutter E2E & Flow Audit | Audit MOB-01 to MOB-05, RelativeCard dead handler, MyRelatives orphan route, cancel popup UI | None | DONE |
| M3 | Performance & PostgreSQL Latency Profiling | Benchmark 17 endpoints latency, slow queries (>200ms), missing index analysis | None | DONE |
| M4 | RBAC & Security Audit | Verify 5 roles, 6 cross-role 401/403 tests, IDOR vulnerability audit on appointments | None | DONE |
| M5 | Test Data Cleanup & DB State Verification | Execute 18-step DAG cleanup script, verify database zero-pollution state | M1, M2, M3, M4 | DONE |
| M6 | Final Verification, Forensic Audit & Deliverables Synthesis | Forensic auditor check, compile Functional Matrix, Latency Report, Security Report, Cleanup Confirmation | M1, M2, M3, M4, M5 | DONE |

## Interface Contracts
### Web/Mobile ↔ Backend REST API
- Base URL: `http://localhost:5036` (or `--dart-define=API_BASE_URL` / Next.js rewrites)
- Auth Header: `Authorization: Bearer <jwt_token>`
- Standard Response Envelope: `ApiResponse<T>` with `{ success, data, error: { code, message, details } }`
- Error Status Codes: `401 Unauthorized` for missing/invalid token, `403 Forbidden` for role mismatch or deactivated user, `404 NotFound` for unowned private entities, `409 Conflict` for duplicate resources.

### Backend ↔ PostgreSQL
- Protocol: PostgreSQL Wire Protocol (port 5432)
- Target: Supabase pooler `aws-0-ap-southeast-1.pooler.supabase.com:5432` / local PostgreSQL 18
- Profiling Extension: `pg_stat_statements` 1.11

## Code Layout
- Web Frontend: `d:\ADSUS\SEP490_ADSUS\adsus-fe`
- Backend API: `d:\ADSUS\SEP490_ADSUS\ADSUS_BE`
- Mobile App: `d:\ADSUS\SEP490_ADSUS\ADSUS_Mobile`
- Backend Unit Tests: `d:\ADSUS\SEP490_ADSUS\ADSUS_BE.UnitTests`
- Metadata & Reports: `d:\ADSUS\SEP490_ADSUS\.agents/`
