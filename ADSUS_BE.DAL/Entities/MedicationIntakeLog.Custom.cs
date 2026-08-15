using System.ComponentModel.DataAnnotations.Schema;
namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bá»• sung cá»™t <c>status</c> mÃ  scaffold khÃ´ng sinh Ä‘Æ°á»£c (enum PostgreSQL).
///
/// Äá»ƒ trong lá»›p partial riÃªng nÃªn cháº¡y láº¡i <c>scaffold --force</c> cÅ©ng khÃ´ng máº¥t â€” file
/// MedicationIntakeLog.cs sinh tá»± Ä‘á»™ng sáº½ bá»‹ ghi Ä‘Ã¨, file nÃ y thÃ¬ khÃ´ng.
///
/// KhÃ´ng cÃ³ "Missed" â€” JOB-01 nháº¯c láº·p láº¡i liÃªn tá»¥c cho tá»›i khi bá»‡nh nhÃ¢n xÃ¡c nháº­n Taken.
/// </summary>
public partial class MedicationIntakeLog
{
    [Column("status")]
    public IntakeStatus Status { get; set; } = IntakeStatus.Pending;
}
