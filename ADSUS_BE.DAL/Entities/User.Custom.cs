using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

// The columns the scaffolder skipped because they are PostgreSQL enums.
// Kept in a separate file (User is a partial class) so a future `scaffold --force`
// cannot wipe them.
public partial class User
{
    /// <summary>
    /// The <c>role</c> column. Decides which area the user is routed to after signing in
    /// (UC-01 BR-03).
    /// </summary>
    [Column("role")]
    public UserRole Role { get; set; }

    /// <summary>
    /// The <c>status</c> column. Only Active can sign in — Deactivated is rejected even when
    /// the password is correct (UC-01 BR-01).
    /// </summary>
    [Column("status")]
    public UserStatus Status { get; set; }

    /// <summary>
    /// Giới tính - chuyển từ patient_profiles (2026-01).
    /// Áp dụng cho tất cả role. Nullable cho phép chưa khai báo.
    /// </summary>
    [Column("gender")]
    public GenderType? Gender { get; set; }
}
