using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bá»• sung cá»™t <c>status</c> mÃ  scaffold khÃ´ng sinh Ä‘Æ°á»£c (enum PostgreSQL).
///
/// Báº£ng appointments thuá»™c Module 8. á»ž Ä‘Ã¢y chá»‰ THÃŠM thuá»™c tÃ­nh Ä‘á»ƒ Dashboard (UC-05) Ä‘áº¿m
/// Ä‘Æ°á»£c tá»· lá»‡ Booked/Cancelled, khÃ´ng Ä‘á»•i gÃ¬ khÃ¡c.
/// </summary>
public partial class Appointment
{
    [Column("status")]
    public AppointmentStatus Status { get; set; }
}
