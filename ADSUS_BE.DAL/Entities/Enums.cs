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
