using ADSUS_BE.BLL.MedicalRecord.DTOs;

namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

public interface IMedicalDictionaryService
{
    Task<IReadOnlyList<MedicalDiseaseResponse>> GetDiseasesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MedicalAllergyTypeResponse>> GetAllergyTypesAsync(CancellationToken ct = default);
}
