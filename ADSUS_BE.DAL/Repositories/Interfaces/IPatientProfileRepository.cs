using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IPatientProfileRepository
{
    /// <summary>Đọc để hiển thị. Có Include(User) vì response cần họ tên/sđt/ngày sinh.</summary>
    Task<PatientProfile?> GetByIdAsync(Guid patientProfileId, CancellationToken ct = default);

    /// <summary>Đọc để sửa — có tracking.</summary>
    Task<PatientProfile?> GetForUpdateAsync(Guid patientProfileId, CancellationToken ct = default);

    /// <summary>Tìm hồ sơ theo tài khoản người dùng (dùng khi bệnh nhân tự xem hồ sơ mình).</summary>
    Task<PatientProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Chặn tạo hồ sơ thứ hai cho cùng một tài khoản (uq_patient_profiles_user).</summary>
    Task<bool> ExistsForUserAsync(Guid userId, CancellationToken ct = default);

    Task<PatientProfile> AddAsync(PatientProfile profile, CancellationToken ct = default);

    Task UpdateAsync(PatientProfile profile, CancellationToken ct = default);
}
