using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung cột <c>status</c> mà scaffold không sinh được (enum PostgreSQL).
///
/// Để trong lớp partial riêng nên chạy lại <c>scaffold --force</c> cũng không mất — file
/// ScheduleSlot.cs sinh tự động sẽ bị ghi đè, file này thì không.
///
/// Không có "Full" — số Appointment/slot không giới hạn (quyết định UCS 3.1, 23/07/2026).
/// </summary>
public partial class ScheduleSlot
{
    [Column("status")]
    public SlotStatus Status { get; set; } = SlotStatus.Open;
}
