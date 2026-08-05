using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

public sealed class DoctorDirectoryService : IDoctorDirectoryService
{
    private readonly IUserRepository _users;

    public DoctorDirectoryService(IUserRepository users) => _users = users;

    public async Task<IReadOnlyList<DoctorSummaryResponse>> ListAsync(CancellationToken ct = default)
    {
        var doctors = await _users.ListActiveDoctorsAsync(ct);

        return doctors
            .Select(u => new DoctorSummaryResponse(u.UserId, u.FullName))
            .ToList();
    }
}
