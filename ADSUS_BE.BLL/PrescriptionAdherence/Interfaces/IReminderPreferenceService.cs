using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

/// <summary>
/// SCR-19 — reminder settings của bệnh nhân (GET/PUT /api/v1/me/reminder-preference).
/// Mỗi bệnh nhân có tối đa 1 dòng preference.
/// </summary>
public interface IReminderPreferenceService
{
    /// <summary>GET — lấy preference hiện tại của bệnh nhân, trả default nếu chưa có dòng.</summary>
    Task<ReminderPreferenceResponse> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>PUT — upsert preference. Tạo mới nếu chưa có, update nếu đã có.</summary>
    Task<ReminderPreferenceResponse> UpsertAsync(
        Guid userId,
        UpdateReminderPreferenceRequest request,
        CancellationToken ct = default);
}
