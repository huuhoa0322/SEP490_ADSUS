namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

public interface ICaseReportService
{
    /// <summary>
    /// UC-12 — dựng file PDF cho một lần khám đã được duyệt. Ném BusinessException (→ 422)
    /// nếu ca chưa CONFIRMED (AF-01).
    /// </summary>
    Task<byte[]> GenerateReportAsync(Guid caseId, CancellationToken ct = default);
}
