using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung cột <c>status</c> mà scaffold không sinh được (enum PostgreSQL).
///
/// Bảng appointments thuộc Module 8. Ở đây chỉ THÊM thuộc tính để Dashboard (UC-05) đếm
/// được tỉ lệ Booked/Cancelled, không đổi gì khác.
/// </summary>
public partial class Appointment
{
    [Column("status")]
    public AppointmentStatus Status { get; set; }
}
