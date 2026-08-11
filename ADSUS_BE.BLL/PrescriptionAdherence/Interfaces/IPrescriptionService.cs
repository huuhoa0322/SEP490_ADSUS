using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

/// <summary>
/// UC-18 — Bác sĩ kê đơn thuốc từ ca đã được duyệt.
/// Handler nhận CreatePrescriptionRequest, validate ca đúng bác sĩ, sinh intake logs,
/// lưu prescription + items vào DB.
/// </summary>
public interface IPrescriptionService
{
    /// <summary>
    /// Tạo đơn thuốc mới. Gọi IntakeLogGenerationService để sinh liều cho mỗi dòng thuốc.
    /// GB-04: kiểm tra doctor tồn tại + Role == Doctor + Status == Active.
    /// GB-01: đơn mới luôn Active (không Draft).
    /// UC-18 BR-01: case phải ở trạng thái Confirmed.
    /// </summary>
    Task<PrescriptionResponse> CreateAsync(
        Guid actorId,
        CreatePrescriptionRequest request,
        CancellationToken ct = default);

    /// <summary>Lấy đơn thuốc mới nhất của một ca khám (dùng cho GET /api/v1/cases/{caseId}/prescription).</summary>
    Task<PrescriptionResponse?> GetByCaseIdAsync(Guid caseId, CancellationToken ct = default);
}