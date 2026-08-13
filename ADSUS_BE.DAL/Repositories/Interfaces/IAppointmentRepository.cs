using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho Appointment (Module 8 — UC-13, UC-14).
/// Patient đặt lịch và xem lịch hẹn của mình.
/// </summary>
public interface IAppointmentRepository
{
    /// <summary>
    /// Danh sách appointment của một bệnh nhân.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListByPatientAsync(
        Guid patientProfileId,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy appointment theo ID (kèm Slot + Doctor).
    /// </summary>
    Task<Appointment?> GetByIdAsync(Guid appointmentId, CancellationToken ct = default);

    /// <summary>
    /// Tạo appointment mới.
    /// </summary>
    Task<Appointment> CreateAsync(Appointment appointment, CancellationToken ct = default);

    /// <summary>
    /// Update appointment (dùng khi cancel).
    /// </summary>
    Task UpdateAsync(Appointment appointment, CancellationToken ct = default);
}
