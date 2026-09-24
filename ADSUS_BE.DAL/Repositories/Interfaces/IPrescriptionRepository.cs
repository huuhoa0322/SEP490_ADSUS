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

    /// <summary>Lấy tất cả đơn của 1 bác sĩ đã kê.</summary>
    Task<IReadOnlyList<Prescription>> ListByDoctorAsync(Guid doctorId, CancellationToken ct = default);

    /// <summary>Lấy đơn mới nhất của 1 ca (dùng khi case detail cần hiển thị đơn).</summary>
    Task<Prescription?> GetByCaseIdAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>Lấy tất cả đơn của 1 ca, sắp xếp đơn mới nhất trước.</summary>
    Task<IReadOnlyList<Prescription>> ListByCaseAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>Thêm 1 đơn (chưa bao gồm items) vào change tracker. Controller gọi SaveChangesAsync.</summary>
    Task AddAsync(Prescription prescription, CancellationToken ct = default);

    /// <summary>
    /// Đơn đang hiệu lực (Active) của một ca, kèm các dòng thuốc → Medicine → quy cách đóng gói →
    /// đơn vị tính (đủ cho lập hoá đơn, xuất kho). Chỉ đọc.
    /// </summary>
    Task<Prescription?> GetActiveByCaseWithItemsAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>Đơn đang hiệu lực của một ca kèm các dòng thuốc và Case (để biết hồ sơ bệnh nhân khi sinh lịch uống). Chỉ đọc.</summary>
    Task<Prescription?> GetActiveByCaseForIntakeScheduleAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>Ca đã có đơn đang hiệu lực (Active) chưa.</summary>
    Task<bool> ExistsActiveByCaseAsync(Guid caseId, CancellationToken ct = default);
}
