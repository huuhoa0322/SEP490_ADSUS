using NpgsqlTypes;

namespace ADSUS_BE.DAL.Entities;

// Enum PostgreSQL không scaffold tự động được ("Enum column cannot be scaffolded"), nên khai
// báo thủ công ở đây. [PgName] ánh xạ mỗi giá trị sang đúng nhãn viết hoa trong DB — thiếu
// nó thì Npgsql tự đổi sang snake_case và không khớp.

/// <summary>
/// Vai trò tài khoản — enum <c>user_role</c> trong DB.
/// Thứ tự khai báo phải khớp thứ tự trong DB: ADMIN, DOCTOR, STAFF, PATIENT, PHARMACIST.
/// STAFF có quyền giống hệt DOCTOR (theo quyết định ghi đè PRD trong UCS).
/// </summary>
public enum UserRole
{
    [PgName("ADMIN")] Admin,
    [PgName("DOCTOR")] Doctor,
    [PgName("STAFF")] Staff,
    [PgName("PATIENT")] Patient,
    [PgName("PHARMACIST")] Pharmacist,
}

/// <summary>
/// Trạng thái tài khoản — enum <c>user_status</c> trong DB.
/// Chỉ tài khoản Active mới đăng nhập được (UC-01 BR-01). Deactivated là trạng thái cuối:
/// không bao giờ đảo ngược và bản ghi không bao giờ bị xoá cứng.
/// </summary>
public enum UserStatus
{
    [PgName("ACTIVE")] Active,
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
/// Trạng thái lịch hẹn — enum <c>appointment_status</c> trong DB (Module 8).
/// Flow: Booked → Approved (nurse checkin) → Completed (doctor end case)
/// </summary>
public enum AppointmentStatus
{
    [PgName("BOOKED")] Booked,
    [PgName("CANCELLED")] Cancelled,
    [PgName("COMPLETED")] Completed,   // Nurse check-in thì appointment chuyển sang COMPLETED
    [PgName("NO_SHOW")] NoShow,        // Tự động hủy khi bệnh nhân không check-in trong grace time
}

/// <summary>
/// Trạng thái ca khám — enum <c>case_status</c> trong DB (Module 4, lõi đề tài).
/// Vòng đời:
///   BOOKED (từ mobile) → IN_PROGRESS (checkin) → CONFIRMED → END (có đơn thuốc)
///   BOOKED → CANCELLED (no-show hoặc hủy lịch)
/// END là trạng thái cuối — không có đường lùi (GB-01).
/// CREATED giữ lại để tương thích với DB cũ.
/// </summary>
public enum CaseStatus
{
    [PgName("BOOKED")] Booked,            // Từ mobile booking (chưa checkin)
    [PgName("IN_PROGRESS")] InProgress,   // Checkin thành công, đang khám
    [PgName("CONFIRMED")] Confirmed,      // Bác sĩ kết luận
    [PgName("END")] End,                  // Hoàn thành (có đơn thuốc)
    [PgName("CANCELLED")] Cancelled,       // NoShow hoặc bệnh nhân hủy
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
    [PgName("CANCELLED")] Cancelled,
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

/// <summary>
/// Khung giờ nhắc uống thuốc — enum <c>reminder_slot</c> trong DB.
/// Persisted vào prescription_items.schedule_slots (array column).
/// </summary>
public enum ReminderSlot
{
    [PgName("MORNING")] Morning,
    [PgName("NOON")] Noon,
    [PgName("EVENING")] Evening,
}

public enum HealthLogType
{
    [PgName("EXERCISE")] Exercise,
    [PgName("DIET")] Diet,
}

public enum MedicineStatus
{
    [PgName("ACTIVE")] Active,
    [PgName("INACTIVE")] Inactive,
}

/// <summary>
/// Vai trò trong hội thoại chatbot — enum <c>chat_role</c> trong DB.
/// Phân biệt lượt hỏi (USER) và lượt trả lời (ASSISTANT).
/// </summary>
public enum ChatRole
{
    [PgName("USER")] User,
    [PgName("ASSISTANT")] Assistant,
}

/// <summary>
/// Trạng thái notification — enum <c>notification_status</c> trong DB (Module 9).
/// </summary>
public enum NotificationStatus
{
    [PgName("SENT")] Sent,
    [PgName("DELIVERED")] Delivered,
    [PgName("FAILED")] Failed,
    [PgName("READ")] Read,
    [PgName("UNREAD")] Unread,
}

/// <summary>
/// Loại notification — enum <c>notification_type</c> trong DB (Module 9).
/// Dùng để phân biệt notification và xác định navigation khi bấm vào.
/// </summary>
public enum NotificationType
{
    [PgName("general")] General,
    [PgName("medication_reminder")] MedicationReminder,
    [PgName("medication_confirmation")] MedicationConfirmation,
    [PgName("appointment_booking")] AppointmentBooking,
    [PgName("appointment_reminder")] AppointmentReminder,
    [PgName("appointment_cancellation")] AppointmentCancellation,
    [PgName("healthlog_reminder")] HealthlogReminder,
    [PgName("medical_record_added")] MedicalRecordAdded,
    [PgName("blog_new_post")] BlogNewPost,
    [PgName("weekly_health_report")] WeeklyHealthReport,
    [PgName("adherence_summary")] AdherenceSummary,
    [PgName("inventory_alert")] InventoryAlert,
}

public enum InventoryTxnType
{
    [PgName("IMPORT")] Import,
    [PgName("DISPENSE")] Dispense,
    [PgName("ADJUSTMENT")] Adjustment,
}

public enum InvoiceStatus
{
    [PgName("PENDING")] PENDING,
    [PgName("PAID")] PAID,
    [PgName("CANCELLED")] CANCELLED,
}

public enum PaymentMethod
{
    [PgName("CASH")] CASH,
    [PgName("BANK_TRANSFER")] BANK_TRANSFER,
}

public enum ShiftRequestType
{
    [PgName("LEAVE")] Leave,
    [PgName("OVERTIME")] Overtime,
}

public enum ShiftRequestStatus
{
    [PgName("PENDING")] Pending,
    [PgName("APPROVED")] Approved,
    [PgName("REJECTED")] Rejected,
}

public enum ShiftType
{
    [PgName("MORNING")] Morning,
    [PgName("AFTERNOON")] Afternoon,
    [PgName("EVENING")] Evening,
    [PgName("FULL_DAY")] FullDay,
}

/// <summary>
/// Trạng thái refresh token — dùng RevokedAt nullable + ExpiresAt để xác định trạng thái thực tế.
/// Enum này thống nhất style với các entity khác nhưng không dùng làm cột DB.
/// </summary>
public enum RefreshTokenStatus
{
    [PgName("ACTIVE")] Active,
    [PgName("REVOKED")] Revoked,
}
