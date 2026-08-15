using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bá»• sung cá»™t <c>status</c> mÃ  scaffold khÃ´ng sinh Ä‘Æ°á»£c (enum PostgreSQL).
///
/// Äá»ƒ trong lá»›p partial riÃªng nÃªn cháº¡y láº¡i <c>scaffold --force</c> cÅ©ng khÃ´ng máº¥t â€” file
/// ScheduleSlot.cs sinh tá»± Ä‘á»™ng sáº½ bá»‹ ghi Ä‘Ã¨, file nÃ y thÃ¬ khÃ´ng.
///
/// KhÃ´ng cÃ³ "Full" â€” sá»‘ Appointment/slot khÃ´ng giá»›i háº¡n (quyáº¿t Ä‘á»‹nh UCS 3.1, 23/07/2026).
/// </summary>
public partial class ScheduleSlot
{
    [Column("status")]
    public SlotStatus Status { get; set; } = SlotStatus.Open;
}
