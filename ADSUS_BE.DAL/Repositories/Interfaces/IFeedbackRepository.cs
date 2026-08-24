using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository cho ServiceFeedback. GB-03: KHÔNG có Remove/Delete.
/// </summary>
public interface IFeedbackRepository
{
    /// <summary>Thêm feedback mới.</summary>
    Task<ServiceFeedback> AddAsync(ServiceFeedback feedback, CancellationToken ct = default);

    /// <summary>Lấy tất cả feedback (Admin).</summary>
    Task<IReadOnlyList<ServiceFeedback>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Lấy feedback theo ID (kèm PatientProfile).</summary>
    Task<ServiceFeedback?> GetByIdAsync(Guid feedbackId, CancellationToken ct = default);

    /// <summary>Lấy feedback theo case ID (FT-37 — ca khám).</summary>
    Task<ServiceFeedback?> GetByCaseIdAsync(Guid caseId, CancellationToken ct = default);
}
