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

// ------------------------------------------------------------------------------------
// Hai enum dưới đây thuộc bảng của module KHÁC (ai_results của Module 5, appointments của
// Module 8), nhưng phải khai ở đây vì Dashboard (UC-05, Module 3) cần đếm theo trạng thái.
//
// Chỉ THÊM thuộc tính, không đổi hành vi gì của hai module đó. Ai làm Module 5 / Module 8
// dùng lại luôn, ĐỪNG khai thêm bản thứ hai — trùng tên enum là Npgsql báo lỗi lúc chạy.
//
// Nhãn lấy đúng từ khai báo scaffold trong AppDbContext.cs (dòng 56-57), là ảnh chụp của
// enum thật trong database.
// ------------------------------------------------------------------------------------

/// <summary>
/// Trạng thái một lần chạy AI — enum <c>ai_result_status</c> trong DB.
///
/// Bắt đầu ở PendingReview và bệnh nhân KHÔNG thấy được cho tới khi bác sĩ xác nhận
/// (GB-05). Dashboard dùng để tính tỉ lệ Confirmed / Rejected.
/// </summary>
public enum AiResultStatus
{
    [PgName("PENDING_REVIEW")] PendingReview,
    [PgName("CONFIRMED")] Confirmed,
    [PgName("REJECTED")] Rejected,
}

/// <summary>
/// Trạng thái lịch hẹn — enum <c>appointment_status</c> trong DB.
/// Chỉ có Booked → Cancelled; không có trạng thái "đã khám xong" (theo Glossary của UCS).
/// </summary>
public enum AppointmentStatus
{
    [PgName("BOOKED")] Booked,
    [PgName("CANCELLED")] Cancelled,
}
