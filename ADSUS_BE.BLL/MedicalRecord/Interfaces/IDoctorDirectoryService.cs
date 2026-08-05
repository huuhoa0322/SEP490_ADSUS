using ADSUS_BE.BLL.MedicalRecord.DTOs;

namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

/// <summary>UC-07 GB-04 — tra danh sách Bác sĩ để gán người phụ trách ca khám.</summary>
public interface IDoctorDirectoryService
{
    Task<IReadOnlyList<DoctorSummaryResponse>> ListAsync(CancellationToken ct = default);
}
