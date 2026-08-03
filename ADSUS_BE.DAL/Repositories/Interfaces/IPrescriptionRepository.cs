using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho Prescription aggregate root (header). Chi tiết thuốc nằm ở
/// PrescriptionItem; intake logs nằm ở MedicationIntakeLog — truy cập qua repos khác.
/// Tất cả method là async + nhận CancellationToken, không bao giờ Remove() (GB-03).
/// </summary>
public interface IPrescriptionRepository
{
    /// <summary>Lấy 1 đơn theo ID (kèm items + medicine + doctor navigation).</summary>
    Task<Prescription?> GetByIdAsync(Guid prescriptionId, CancellationToken ct = default);

    /// <summary>Lấy tất cả đơn của 1 bệnh nhân, sắp xếp đơn mới nhất trước.</summary>
    Task<IReadOnlyList<Prescription>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>Module 7 UC-11: danh sách phân trang, filter theo status/date, filter theo Case.PatientProfileId.</summary>
    Task<IReadOnlyList<Prescription>> ListByPatientPagedAsync(
        Guid patientProfileId,
        DateOnly? fromDate,
        DateOnly? toDate,
        IReadOnlyCollection<PrescriptionStatus>? statuses,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>Module 7 UC-11: đếm tổng để phân trang — đi cùng ListByPatientPagedAsync.</summary>
    Task<int> CountByPatientAsync(
        Guid patientProfileId,
        DateOnly? fromDate,
        DateOnly? toDate,
        IReadOnlyCollection<PrescriptionStatus>? statuses,
        CancellationToken ct = default);

    /// <summary>Module 7 UC-18 BR-03: kiểm tra case đã có đơn ACTIVE chưa trước khi kê.</summary>
    Task<bool> HasActiveForCaseAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>Lấy tất cả đơn của 1 bác sĩ đã kê.</summary>
    Task<IReadOnlyList<Prescription>> ListByDoctorAsync(Guid doctorId, CancellationToken ct = default);

    /// <summary>Thêm 1 đơn (chưa bao gồm items) vào change tracker. Controller gọi SaveChangesAsync.</summary>
    Task AddAsync(Prescription prescription, CancellationToken ct = default);
}
