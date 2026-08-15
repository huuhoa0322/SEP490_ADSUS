using System.ComponentModel.DataAnnotations.Schema;
namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bá»• sung cá»™t <c>status</c> mÃ  scaffold khÃ´ng sinh Ä‘Æ°á»£c (enum PostgreSQL).
///
/// Äá»ƒ trong lá»›p partial riÃªng nÃªn cháº¡y láº¡i <c>scaffold --force</c> cÅ©ng khÃ´ng máº¥t â€” file
/// Case.cs sinh tá»± Ä‘á»™ng sáº½ bá»‹ ghi Ä‘Ã¨, file nÃ y thÃ¬ khÃ´ng.
///
/// VÃ²ng Ä‘á»i má»™t chiá»u: Created â†’ Analyzed â†’ Confirmed (UC-19).
/// </summary>
public partial class Case
{
    [Column("status")]
    public CaseStatus Status { get; set; } = CaseStatus.Created;
}
