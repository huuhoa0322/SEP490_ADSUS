using NpgsqlTypes;

namespace ADSUS_BE.DAL.Entities;

// Các enum PostgreSQL không scaffold tự động được ("Enum column cannot be scaffolded"),
// nên khai báo thủ công ở đây. [PgName] ánh xạ sang đúng nhãn viết hoa trong DB —
// nếu bỏ đi, Npgsql sẽ tự đổi sang snake_case và không khớp.

/// <summary>
/// Vai trò tài khoản — enum user_role trong DB.
/// NURSE đã được chốt trong UCS nhưng chưa thêm vào DB, sẽ bổ sung sau.
/// </summary>
public enum UserRole
{
    [PgName("ADMIN")] Admin,
    [PgName("DOCTOR")] Doctor,
    [PgName("PATIENT")] Patient,
}

/// <summary>
/// Trạng thái tài khoản — enum user_status trong DB.
/// Chỉ Active mới đăng nhập được (UC-01 BR-01). Deactivated là trạng thái cuối,
/// không bao giờ quay lại được và không bao giờ xoá cứng bản ghi.
/// </summary>
public enum UserStatus
{
    [PgName("ACTIVE")] Active,
    [PgName("LOCKED")] Locked,
    [PgName("DEACTIVATED")] Deactivated,
}
