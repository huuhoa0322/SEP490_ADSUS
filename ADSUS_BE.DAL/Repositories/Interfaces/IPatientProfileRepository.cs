using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Module 7: lookup bệnh nhân ở tầng service để trả về tên + thông tin context
/// trong PrescriptionDetailResponse. Chỉ đọc, không có Add/Update.
/// </summary>
public interface IPatientProfileRepository
{
    Task<PatientProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
