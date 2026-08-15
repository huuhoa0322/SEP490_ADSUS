using System.ComponentModel.DataAnnotations.Schema;
namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bá»• sung cá»™t <c>status</c> mÃ  scaffold khÃ´ng sinh Ä‘Æ°á»£c (enum PostgreSQL).
///
/// Äá»ƒ trong lá»›p partial riÃªng nÃªn cháº¡y láº¡i <c>scaffold --force</c> cÅ©ng khÃ´ng máº¥t â€” file
/// Prescription.cs sinh tá»± Ä‘á»™ng sáº½ bá»‹ ghi Ä‘Ã¨, file nÃ y thÃ¬ khÃ´ng.
///
/// Completed suy ra khi má»i liá»u thuá»™c Ä‘Æ¡n Ä‘Ã£ Taken (UC-17).
/// </summary>
public partial class Prescription
{
    [Column("status")]
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Active;
}
