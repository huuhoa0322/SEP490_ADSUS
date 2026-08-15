using System.ComponentModel.DataAnnotations.Schema;
namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bá»• sung cá»™t <c>status</c> mÃ  scaffold khÃ´ng sinh Ä‘Æ°á»£c (enum PostgreSQL).
///
/// Äá»ƒ trong lá»›p partial riÃªng nÃªn cháº¡y láº¡i <c>scaffold --force</c> cÅ©ng khÃ´ng máº¥t â€” file
/// AiModelVersion.cs sinh tá»± Ä‘á»™ng sáº½ bá»‹ ghi Ä‘Ã¨, file nÃ y thÃ¬ khÃ´ng.
///
/// Chá»‰ 1 phiÃªn báº£n ACTIVE táº¡i má»™t thá»i Ä‘iá»ƒm (UC-20) â€” kÃ­ch hoáº¡t báº£n má»›i tá»± chuyá»ƒn báº£n Ä‘ang
/// ACTIVE vá» INACTIVE.
/// </summary>
public partial class AiModelVersion
{
    [Column("status")]
    public ModelVersionStatus Status { get; set; } = ModelVersionStatus.Inactive;
}
