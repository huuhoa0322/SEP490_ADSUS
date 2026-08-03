using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;

namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

public interface ICaseService
{
    Task<IReadOnlyList<UltrasoundImageResponse>> ListImagesAsync(
        Guid caseId, CancellationToken ct = default);

    /// <summary>#23 cho Bác sĩ/Điều dưỡng — bản đầy đủ.</summary>
    Task<CaseResponse> GetForStaffAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// #23 cho Bệnh nhân — chỉ ca của chính họ và chỉ khi đã CONFIRMED.
    /// Không thoả điều kiện thì ném ResourceNotFoundException (404), KHÔNG phải 403.
    /// </summary>
    Task<PatientCaseResponse> GetForPatientAsync(
        Guid caseId, Guid callerUserId, CancellationToken ct = default);

    Task<PagedResult<CaseSummaryResponse>> ListByPatientProfileAsync(
        Guid patientProfileId,
        string? status,
        string sortOrder,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>#25 — danh sách lần khám của chính người gọi, luôn ép về CONFIRMED.</summary>
    Task<PagedResult<CaseSummaryResponse>> ListMineAsync(
        Guid callerUserId, int page, int pageSize, CancellationToken ct = default);
}
