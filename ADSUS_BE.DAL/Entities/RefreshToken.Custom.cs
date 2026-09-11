namespace ADSUS_BE.DAL.Entities;

public partial class RefreshToken
{
    /// <summary>
    /// Trạng thái token — computed từ RevokedAt để thống nhất style với các entity khác.
    /// </summary>
    public RefreshTokenStatus Status =>
        RevokedAt.HasValue ? RefreshTokenStatus.Revoked : RefreshTokenStatus.Active;
}
