using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

public partial class ShiftRequest
{
    public ShiftRequestType RequestType { get; set; }

    public ShiftType ShiftType { get; set; }

    public ShiftRequestStatus Status { get; set; } = ShiftRequestStatus.Pending;
}
