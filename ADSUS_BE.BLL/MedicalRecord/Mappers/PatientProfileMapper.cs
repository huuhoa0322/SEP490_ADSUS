using ADSUS_BE.DAL.Entities;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;

namespace ADSUS_BE.BLL.MedicalRecord.Mappers;

/// <summary>
/// Liệt kê tường minh từng field. Field nào không xuất hiện trong return chính là cơ chế
/// bảo đảm field đó không bao giờ lọt ra ngoài — đừng thay bằng mapper generic/reflection,
/// làm vậy là mất luôn cơ chế này.
/// </summary>
public static class PatientProfileMapper
{
    public static PatientProfileResponse ToResponse(PatientProfile profile) => new(
        PatientProfileId: profile.PatientProfileId,
        PatientUserId: profile.UserId,
        FullName: profile.User?.FullName ?? string.Empty,
        Phone: profile.User?.Phone ?? string.Empty,
        DateOfBirth: profile.User?.DateOfBirth,
        Gender: profile.Gender.ToApiString(),
        MedicalHistory: profile.MedicalHistory,
        Allergies: profile.Allergies,
        CreatedBy: profile.CreatedBy,
        CreatedAt: profile.CreatedAt,
        UpdatedAt: profile.UpdatedAt);
}
