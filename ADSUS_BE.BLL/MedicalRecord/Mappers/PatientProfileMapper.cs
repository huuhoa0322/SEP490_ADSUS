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
        Diseases: profile.PatientDiseases?.Select(d => new PatientDiseaseResponse(
            DiseaseId: d.DiseaseId,
            DiseaseName: d.Disease?.Name ?? string.Empty,
            IsOther: d.Disease?.IsOther ?? false,
            Note: d.Note)).ToList() ?? new List<PatientDiseaseResponse>(),
        Allergies: profile.PatientAllergies?.Select(a => new PatientAllergyResponse(
            AllergyTypeId: a.AllergyTypeId,
            AllergyName: a.AllergyType?.Name ?? string.Empty,
            IsOther: a.AllergyType?.IsOther ?? false,
            Note: a.Note)).ToList() ?? new List<PatientAllergyResponse>(),
        CreatedBy: profile.CreatedBy,
        CreatedAt: profile.CreatedAt,
        UpdatedAt: profile.UpdatedAt);
}
