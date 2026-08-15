using System.ComponentModel.DataAnnotations.Schema;
namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bá»• sung cá»™t <c>gender</c> mÃ  scaffold khÃ´ng sinh Ä‘Æ°á»£c (enum PostgreSQL).
///
/// Äá»ƒ trong lá»›p partial riÃªng nÃªn cháº¡y láº¡i <c>scaffold --force</c> cÅ©ng khÃ´ng máº¥t â€” file
/// PatientProfile.cs sinh tá»± Ä‘á»™ng sáº½ bá»‹ ghi Ä‘Ã¨, file nÃ y thÃ¬ khÃ´ng.
///
/// Thuá»™c tÃ­nh nghiá»‡p vá»¥ cá»§a Patient Profile (PRD Â§2.2.b): giá»›i tÃ­nh, tiá»n sá»­ bá»‡nh, dá»‹ á»©ng,
/// bÃ¡c sÄ© láº­p há»“ sÆ¡.
/// </summary>
public partial class PatientProfile
{
    [Column("gender")]
    public GenderType Gender { get; set; } = GenderType.Female;
}
