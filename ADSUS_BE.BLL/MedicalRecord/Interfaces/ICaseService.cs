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

    /// <summary>#24 — cho Bác sĩ/Điều dưỡng (Web SCR-12). Có CreatedAt, xem StaffCaseSummaryResponse.</summary>
    Task<PagedResult<StaffCaseSummaryResponse>> ListByPatientProfileAsync(
        Guid patientProfileId,
        string? status,
        string sortOrder,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>#25 — danh sách lần khám của chính người gọi, luôn ép về CONFIRMED.</summary>
    Task<PagedResult<CaseSummaryResponse>> ListMineAsync(
        Guid callerUserId, int page, int pageSize, CancellationToken ct = default);

    Task<CaseResponse> CreateAsync(
        CreateCaseRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<UltrasoundImageResponse>> AddImagesAsync(
        Guid caseId, AddUltrasoundImagesRequest request, CancellationToken ct = default);

    /// <summary>
    /// Thêm 07/08/2026 — "Lưu kết luận". Chỉ lưu nội dung, KHÔNG đổi trạng thái ca — sửa lại
    /// được nhiều lần. Cùng hai điều kiện với ConfirmAsync: chỉ Bác sĩ phụ trách CA NÀY (GB-04),
    /// và ca chưa CONFIRMED (GB-01/P2 — ca đã khoá thì không sửa được nữa, kể cả chỉ lưu nháp).
    /// </summary>
    Task<CaseResponse> SaveConclusionAsync(
        Guid caseId, Guid actingDoctorId, CaseConclusionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Thêm 07/08/2026 — "Kết thúc ca khám". Lưu VÀ khoá ca (CONFIRMED) trong cùng một lần gọi.
    /// Chỉ đúng Bác sĩ phụ trách của ca này mới làm được (GB-04), và chỉ làm được MỘT LẦN — ca
    /// đã CONFIRMED thì từ chối luôn (GB-01/P2, không có đường lùi).
    /// </summary>
    Task<CaseResponse> ConfirmAsync(
        Guid caseId, Guid actingDoctorId, CaseConclusionRequest request, CancellationToken ct = default);
}
