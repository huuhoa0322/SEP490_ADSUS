using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

public partial class AiChatMessage
{
    [Column("role")]
    public ChatRole Role { get; set; }
}
