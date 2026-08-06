namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung cột <c>schedule_slots</c> (reminder_slot[]) mà scaffold không sinh được.
/// Để trong lớp partial riêng nên chạy lại <c>scaffold --force</c> cũng không mất.
/// </summary>
public partial class PrescriptionItem
{
    /// <summary>
    /// Mảng khung giờ uống thuốc cho dòng thuốc này.
    /// Persisted để JOB-01 đọc khi sinh intake logs.
    /// </summary>
    public ReminderSlot[] ScheduleSlots { get; set; } = Array.Empty<ReminderSlot>();
}
