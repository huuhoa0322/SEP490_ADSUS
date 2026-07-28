namespace ADSUS_BE.DAL.Entities;

// Bổ sung 2 cột mà scaffold bỏ sót vì chúng là enum PostgreSQL.
// Để ở file riêng (User là partial class) nên lần scaffold --force sau sẽ không xoá mất.
public partial class User
{
    /// <summary>
    /// Cột role. Quyết định người dùng được điều hướng về đâu sau khi đăng nhập (UC-01 BR-03).
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Cột status. Chỉ Active mới đăng nhập được — Locked và Deactivated đều bị từ chối
    /// dù mật khẩu đúng (UC-01 BR-01).
    /// </summary>
    public UserStatus Status { get; set; }
}
