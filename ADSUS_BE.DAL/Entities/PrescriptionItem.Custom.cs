using System.ComponentModel.DataAnnotations.Schema;
namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bá»• sung cá»™t <c>schedule_slots</c> (reminder_slot[]) mÃ  scaffold khÃ´ng sinh Ä‘Æ°á»£c.
/// Äá»ƒ trong lá»›p partial riÃªng nÃªn cháº¡y láº¡i <c>scaffold --force</c> cÅ©ng khÃ´ng máº¥t.
/// </summary>
public partial class PrescriptionItem
{
    /// <summary>
    /// Máº£ng khung giá» uá»‘ng thuá»‘c cho dÃ²ng thuá»‘c nÃ y.
    /// Persisted Ä‘á»ƒ JOB-01 Ä‘á»c khi sinh intake logs.
    /// </summary>
    [Column("schedule_slots")]
    public ReminderSlot[] ScheduleSlots { get; set; } = Array.Empty<ReminderSlot>();
}
