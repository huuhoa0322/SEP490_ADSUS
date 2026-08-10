using NpgsqlTypes;

namespace ADSUS_BE.DAL.Entities;

// Enum PostgreSQL không scaffold tự động được ("Enum column cannot be scaffolded"), nên khai
// báo thủ công ở đây. [PgName] ánh xạ mỗi giá trị sang đúng nhãn viết hoa trong DB — thiếu
// nó thì Npgsql tự đổi sang snake_case và không khớp.

/// <summary>
/// Vai trò tài khoản — enum <c>user_role</c> trong DB.
/// Thứ tự khai báo phải khớp thứ tự trong DB: ADMIN, DOCTOR, PATIENT, NURSE.
/// NURSE có quyền giống hệt DOCTOR (theo quyết định ghi đè PRD trong UCS).
/// </summary>
public enum UserRole
{
    [PgName("ADMIN")] Admin,
    [PgName("DOCTOR")] Doctor,
    [PgName("PATIENT")] Patient,
    [PgName("NURSE")] Nurse,
}

/// <summary>
/// Trạng thái tài khoản — enum <c>user_status</c> trong DB.
/// Chỉ tài khoản Active mới đăng nhập được (UC-01 BR-01). Deactivated là trạng thái cuối:
/// không bao giờ đảo ngược và bản ghi không bao giờ bị xoá cứng.
/// </summary>
public enum UserStatus
{
    [PgName("ACTIVE")] Active,
    [PgName("LOCKED")] Locked,
    [PgName("DEACTIVATED")] Deactivated,
}

/// <summary>
/// Trạng thái bài viết blog — enum <c>blog_status</c> trong DB.
/// GB-01 (trạng thái một chiều): Draft → Published (không rollback).
/// Bệnh nhân chỉ thấy Published (GB-05).
/// </summary>
public enum BlogPostStatus
{
    [PgName("DRAFT")] Draft,
    [PgName("PUBLISHED")] Published,
}

/// <summary>
/// Trạng thái kết quả chẩn đoán AI — enum <c>ai_result_status</c> trong DB (Module 5).
/// Dùng bởi Dashboard (UC-05) để đếm tỉ lệ Confirmed/Rejected.
/// </summary>
public enum AiResultStatus
{
    [PgName("PENDING_REVIEW")] PendingReview,
    [PgName("CONFIRMED")] Confirmed,
    [PgName("REJECTED")] Rejected,
}

/// <summary>
/// Trạng thái lịch hẹn — enum <c>appointment_status</c> trong DB (Module 8).
/// Dùng bởi Dashboard (UC-05) để đếm tỉ lệ Booked/Cancelled.
/// </summary>
public enum AppointmentStatus
{
    [PgName("BOOKED")] Booked,
    [PgName("CANCELLED")] Cancelled,
}

/// <summary>
/// Trạng thái ca khám — enum <c>case_status</c> trong DB (Module 4, lõi đề tài).
/// Vòng đời một chiều: Created → Analyzed (có kết quả AI) → Confirmed (bác sĩ đã duyệt).
/// </summary>
public enum CaseStatus
{
    [PgName("CREATED")] Created,
    [PgName("ANALYZED")] Analyzed,
    [PgName("CONFIRMED")] Confirmed,
}

/// <summary>
/// Trạng thái phiên bản AI Model — enum <c>model_version_status</c> trong DB (Module 4).
/// Chỉ 1 phiên bản ACTIVE tại một thời điểm (partial unique index) — kích hoạt bản mới tự
/// chuyển bản đang ACTIVE về INACTIVE (rollback).
/// </summary>
public enum ModelVersionStatus
{
    [PgName("ACTIVE")] Active,
    [PgName("INACTIVE")] Inactive,
}

/// <summary>
/// Trạng thái đơn thuốc — enum <c>prescription_status</c> trong DB (Module 5).
/// Completed suy ra khi mọi liều đã Taken (không phải job quét, tính tại thời điểm đọc).
/// </summary>
public enum PrescriptionStatus
{
    [PgName("ACTIVE")] Active,
    [PgName("COMPLETED")] Completed,
}

/// <summary>
/// Trạng thái 1 liều thuốc — enum <c>intake_status</c> trong DB (Module 5).
/// Không có "Missed" — JOB-01 nhắc lặp lại liên tục cho tới khi bệnh nhân xác nhận Taken.
/// </summary>
public enum IntakeStatus
{
    [PgName("PENDING")] Pending,
    [PgName("TAKEN")] Taken,
}

/// <summary>
/// Trạng thái khung giờ khám — enum <c>slot_status</c> trong DB (Module 8).
/// - OPEN: Bác sĩ tạo ca, chưa có bệnh nhân book
/// - BOOKED: Bệnh nhân đã book ca này (1 slot = 1 lịch hẹn)
/// - CLOSED: Bác sĩ đóng ca (không nhận thêm booking)
/// </summary>
public enum SlotStatus
{
    [PgName("OPEN")] Open,
    [PgName("BOOKED")] Booked,
    [PgName("CLOSED")] Closed,
}

/// <summary>
/// Giới tính trong hồ sơ y tế nền — enum <c>gender_type</c> trong DB (Module 4).
/// Thuộc tính nghiệp vụ chính thức của Patient Profile (PRD §2.2.b, UC-06).
/// Cột DB mặc định 'FEMALE', nhưng đó là đặc thù schema — client luôn nên gửi giá trị rõ ràng.
/// </summary>
public enum GenderType
{
    [PgName("FEMALE")] Female,
    [PgName("MALE")] Male,
    [PgName("OTHER")] Other,
}
