using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IMedicalDictionaryRepository
{
    Task<IReadOnlyList<MedicalDisease>> ListDiseasesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MedicalAllergyType>> ListAllergyTypesAsync(CancellationToken ct = default);
}
