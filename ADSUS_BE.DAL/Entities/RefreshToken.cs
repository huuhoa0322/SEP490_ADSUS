namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Lưu refresh token cho JWT token renewal.
/// Hỗ trợ SignalR duy trì kết nối mà không cần user đăng nhập lại.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// Trạng thái token — computed từ RevokedAt để thống nhất style với các entity khác.
    /// </summary>
    public RefreshTokenStatus Status =>
        RevokedAt.HasValue ? RefreshTokenStatus.Revoked : RefreshTokenStatus.Active;

    // Navigation
    public User User { get; set; } = null!;
}
