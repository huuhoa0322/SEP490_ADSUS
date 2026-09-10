using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

public partial class ShiftRequest
{
    [Column("request_type")]
    public ShiftRequestType RequestType { get; set; }

    [Column("shift_type")]
    public ShiftType ShiftType { get; set; }

    [Column("status")]
    public ShiftRequestStatus Status { get; set; } = ShiftRequestStatus.Pending;
}
