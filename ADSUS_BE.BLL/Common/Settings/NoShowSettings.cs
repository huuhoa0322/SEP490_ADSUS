namespace ADSUS_BE.BLL.Common.Settings;

/// <summary>
/// Cấu hình cho chức năng No-Show.
/// </summary>
public sealed class NoShowSettings
{
    /// <summary>
    /// Số phút kể từ khi slot bắt đầu mà patient được phép check-in.
    /// Sau grace time → tự động đánh dấu No-Show.
    /// Default: 15 phút.
    /// </summary>
    public int GraceTimeMinutes { get; set; } = 15;
}
