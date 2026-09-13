namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Bổ sung logic nghiệp vụ và computed properties cho Appointment.
/// </summary>
public partial class Appointment
{
    /// <summary>
    /// Các trạng thái lịch hẹn được tính là active (đang chờ khám hoặc đã check-in chờ vào phòng).
    /// </summary>
    public static readonly AppointmentStatus[] ActiveStatuses =
    [
        AppointmentStatus.Booked,
        AppointmentStatus.CheckedIn
    ];

    /// <summary>
    /// Kiểm tra cuộc hẹn có đang ở trạng thái active hay không.
    /// </summary>
    public bool IsActive => Status == AppointmentStatus.Booked || Status == AppointmentStatus.CheckedIn;
}
