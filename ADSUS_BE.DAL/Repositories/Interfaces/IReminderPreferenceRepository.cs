using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho PatientReminderPreference (SCR-19 reminder settings).
/// Mỗi bệnh nhân có tối đa 1 dòng (unique index trên patient_profile_id).
/// </summary>
public interface IReminderPreferenceRepository
{
    /// <summary>Tìm preference theo patientProfileId. Trả null nếu chưa có.</summary>
    Task<PatientReminderPreference?> GetByPatientProfileIdAsync(
        Guid patientProfileId,
        CancellationToken ct = default);

    /// <summary>Tìm preference theo patientProfileId — có tracking để sửa.</summary>
    Task<PatientReminderPreference?> GetForUpdateAsync(
        Guid patientProfileId,
        CancellationToken ct = default);

    /// <summary>Thêm dòng mới.</summary>
    Task AddAsync(PatientReminderPreference preference, CancellationToken ct = default);

    /// <summary>Update dòng đã tồn tại.</summary>
    Task UpdateAsync(PatientReminderPreference preference, CancellationToken ct = default);
}
