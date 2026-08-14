namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Phần mở rộng cho PatientReminderPreference — các cột mới được thêm khi làm Module 7
/// SCR-19 reminder settings.
/// </summary>
public partial class PatientReminderPreference
{
    /// <summary>
    /// Bật/tắt thông báo nhắc nhở. Mặc định true.
    /// </summary>
    public bool NotifEnabled { get; set; } = true;

    /// <summary>
    /// Giờ nhắc khung sáng. Mặc định 07:00.
    /// </summary>
    public TimeOnly MorningTime { get; set; } = new(7, 0);

    /// <summary>
    /// Giờ nhắc khung trưa. Mặc định 12:00.
    /// </summary>
    public TimeOnly MiddayTime { get; set; } = new(12, 0);

    /// <summary>
    /// Giờ nhắc khung tối. Mặc định 20:00.
    /// </summary>
    public TimeOnly EveningTime { get; set; } = new(20, 0);
}
